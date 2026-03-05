using System;
using System.Collections;
using System.Collections.Generic;
using BodylinkSDK;
using Mediapipe;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using UnityEngine;


/// <summary>
/// Full-body cutout processor
/// </summary>
public class FullBodyCutout : MonoBehaviour
{
    [Range(0, 1)]
    public float maskThreshold = 0.5f;
    public Texture2D[] bodyCutout { get; private set; } = new Texture2D[2];
    private static readonly Queue<Action> actions = new Queue<Action>();


    public void Run(Action action)
    {
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }
    void Start()
    {
        Bodylink.Instance.OnInitialized += OnInit;
    }
    void OnInit()
    {
        Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult += OnPoseLandmarkDetectionOutput;
        StartCoroutine("SetSegmentationMasksEnabled");
    }
    private IEnumerator SetSegmentationMasksEnabled()
    {
        yield return new WaitForEndOfFrame();
        Bodylink.Instance.PoseLandmarkerRunnerInstance.config.OutputSegmentationMasks = true;
        Bodylink.Instance.PoseLandmarkerRunnerInstance.Play();
        Bodylink.Instance.handGestureRunnerInstance.Play();
    }
    void OnDisable()
    {
        Bodylink.Instance.OnInitialized -= OnInit;
        if (Bodylink.Instance.PoseLandmarkerRunnerInstance != null)
            Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult -= OnPoseLandmarkDetectionOutput;
    }

    private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult poseLandmarkerResult, long timestamp)
    {

        if (poseLandmarkerResult.segmentationMasks == null || poseLandmarkerResult.segmentationMasks.Count == 0)
            return;

        var srcTexture = Bodylink.Instance.cameraScreen.imageSource?.GetCurrentTexture();
        if (srcTexture == null)
            return;


        CreateCutoutAsync(
            srcTexture,
            poseLandmarkerResult.segmentationMasks[0],
            maskThreshold: maskThreshold,
            mirrorMaskHorizontally: true,
            callback: (cutoutTexture) =>
            {
                bodyCutout[0] = cutoutTexture;
            });

        if (Bodylink.Instance.numberOfPlayers > 1 && poseLandmarkerResult.segmentationMasks.Count > 1)
        {
            CreateCutoutAsync(
               srcTexture,
               poseLandmarkerResult.segmentationMasks[1],
               maskThreshold: maskThreshold,
               mirrorMaskHorizontally: true,
               callback: (cutoutTexture) =>
               {
                   bodyCutout[1] = cutoutTexture;
               });
        }

    }
    void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
                actions.Dequeue()?.Invoke();
        }


    }
    /// <summary>
    /// Entry point for cutout creation.
    /// Call this from any thread safely.
    /// </summary>
    private void CreateCutoutAsync(Texture sourceTexture, Image segmentationMask, float maskThreshold = 0.5f, bool mirrorMaskHorizontally = false, Action<Texture2D> callback = null)
    {
        if (sourceTexture == null || segmentationMask == null)
        {
            Debug.LogError("Source texture or segmentation mask is null.");
            return;
        }

        // Ensure execution on the main thread
        Run(() =>
        {
            var cutoutTexture = CreateFullBodyCutout(sourceTexture, segmentationMask, maskThreshold, mirrorMaskHorizontally);
            callback?.Invoke(cutoutTexture);
        });
    }

    /// <summary>
    /// Core function that creates a full-body cutout with alpha from segmentation mask
    /// Must be called on the main thread
    /// </summary>
    private Texture2D CreateFullBodyCutout(Texture sourceTexture, Image segmentationMask, float maskThreshold, bool mirrorMaskHorizontally)
    {
        try
        {
            var readableSource = CopyToTexture2D(sourceTexture);

            int outputWidth = readableSource.width;
            int outputHeight = readableSource.height;

            int maskWidth = segmentationMask.Width();
            int maskHeight = segmentationMask.Height();

            if (maskWidth <= 0 || maskHeight <= 0)
                throw new InvalidOperationException("Segmentation mask has no size.");

            var maskValues = new float[maskWidth * maskHeight];
            if (!segmentationMask.TryReadChannelNormalized(0, maskValues, mirrorMaskHorizontally))
                throw new InvalidOperationException("Unable to read segmentation mask channel.");

            var sourcePixels = readableSource.GetPixels32();
            var cutoutPixels = new Color32[sourcePixels.Length];
            float threshold = Mathf.Clamp01(maskThreshold);

            // Precompute u and v ratios per column and row
            float[] uRatios = new float[outputWidth];
            for (int x = 0; x < outputWidth; x++)
                uRatios[x] = outputWidth > 1 ? (float)x / (outputWidth - 1) : 0f;

            float[] vRatios = new float[outputHeight];
            for (int y = 0; y < outputHeight; y++)
                vRatios[y] = outputHeight > 1 ? (float)y / (outputHeight - 1) : 0f;

            for (int y = 0; y < outputHeight; y++)
            {
                int maskY = Mathf.Clamp(Mathf.RoundToInt(vRatios[y] * (maskHeight - 1)), 0, maskHeight - 1);

                for (int x = 0; x < outputWidth; x++)
                {
                    int maskX = Mathf.Clamp(Mathf.RoundToInt(uRatios[x] * (maskWidth - 1)), 0, maskWidth - 1);
                    int maskIndex = maskY * maskWidth + maskX;
                    float maskValue = Mathf.Clamp01(maskValues[maskIndex]);

                    int pixelIndex = y * outputWidth + x;
                    int writeIndex = mirrorMaskHorizontally ? y * outputWidth + (outputWidth - 1 - x) : pixelIndex;

                    if (maskValue < threshold)
                    {
                        cutoutPixels[writeIndex] = new Color32(0, 0, 0, 0);
                    }
                    else
                    {
                        var sourcePixel = sourcePixels[pixelIndex];
                        byte alpha = (byte)(maskValue * 255f);
                        cutoutPixels[writeIndex] = new Color32(sourcePixel.r, sourcePixel.g, sourcePixel.b, alpha);
                    }
                }
            }

            Texture2D cutout = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            cutout.SetPixels32(cutoutPixels);
            cutout.Apply();
            return cutout;
        }
        catch (Exception e)
        {
            return null;
        }
    }


    /// <summary>
    /// Copies a texture to a readable Texture2D.
    /// Must be called on the main thread.
    /// </summary>
    private static Texture2D CopyToTexture2D(Texture sourceTexture)
    {
        int width = sourceTexture.width;
        int height = sourceTexture.height;

        RenderTexture tempRt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        RenderTexture prevActive = RenderTexture.active;

        Graphics.Blit(sourceTexture, tempRt);
        RenderTexture.active = tempRt;

        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
        readable.Apply();

        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(tempRt);

        return readable;
    }
}
