using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BodylinkSDK
{
    public class WebCamVideoRecording : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private int _cameraIndex = 0;
        [SerializeField] private int _requestedWidth = 1280;
        [SerializeField] private int _requestedHeight = 720;
        [SerializeField] private int _requestedFps = 30;
        [SerializeField] private bool _startCameraOnStart = true;

        [Header("Recording Settings")]
        [SerializeField] private bool _startRecordingOnStart = false;
        [SerializeField, Min(0f), Tooltip("0 = manual start/stop. Values above 0 auto-stop recording after this many seconds.")]
        private float _recordingTimeSeconds = 0f;
        [SerializeField, Range(1, 60)] private int _recordingFps = 30;
        [SerializeField, Range(1, 100)] private int _jpgQuality = 75;
        [SerializeField] private string _outputFolderName = "Videos";
        [SerializeField] private string _fileNamePrefix = "webcam";

        private WebCamTexture _webCamTexture;
        private Texture2D _frameTexture;
        private Color32[] _pixelBuffer;
        private Coroutine _cameraStartupRoutine;
        private AviMjpegWriter _aviWriter;
        private bool _cameraReady;
        private bool _pendingRecordingStart;
        private float _frameInterval;
        private float _nextFrameTime;
        private float _recordingStopAtTime = -1f;

        public bool IsCameraRunning => _webCamTexture != null && _webCamTexture.isPlaying;
        public bool IsRecording => _aviWriter != null;
        public string LastSavedFilePath { get; private set; }
        public Texture CameraTexture => _webCamTexture;

        private void Start()
        {
            if (_startCameraOnStart)
            {
                StartCamera();
            }

            if (_startRecordingOnStart)
            {
                StartRecording();
            }
        }

        private void Update()
        {
            if (_aviWriter == null)
            {
                return;
            }

            if (_recordingStopAtTime > 0f && Time.unscaledTime >= _recordingStopAtTime)
            {
                StopRecording();
                return;
            }

            if (!_cameraReady || _webCamTexture == null)
            {
                return;
            }

            if (!_webCamTexture.didUpdateThisFrame)
            {
                return;
            }

            if (Time.unscaledTime < _nextFrameTime)
            {
                return;
            }

            CaptureAndWriteFrame();
            _nextFrameTime += _frameInterval;

            if (_nextFrameTime < Time.unscaledTime - _frameInterval)
            {
                _nextFrameTime = Time.unscaledTime + _frameInterval;
            }
        }

        private void OnDisable()
        {
            StopRecording();
            StopCamera();
        }

        public void StartCamera()
        {
            if (_cameraStartupRoutine != null || IsCameraRunning)
            {
                return;
            }

            _cameraStartupRoutine = StartCoroutine(StartCameraRoutine());
        }

        public void StopCamera()
        {
            _cameraReady = false;
            _pendingRecordingStart = false;

            if (_cameraStartupRoutine != null)
            {
                StopCoroutine(_cameraStartupRoutine);
                _cameraStartupRoutine = null;
            }

            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying)
                {
                    _webCamTexture.Stop();
                }

                Destroy(_webCamTexture);
                _webCamTexture = null;
            }

            if (_frameTexture != null)
            {
                Destroy(_frameTexture);
                _frameTexture = null;
            }

            _pixelBuffer = null;
        }

        public void StartRecording()
        {
            if (_aviWriter != null)
            {
                return;
            }

            if (!_cameraReady || !IsCameraRunning)
            {
                _pendingRecordingStart = true;
                StartCamera();
                Debug.Log("[WebCamVideoRecording] Waiting for camera initialization before starting recording.");
                return;
            }

            CreateWriterAndStart();
        }

        public void StopRecording()
        {
            _recordingStopAtTime = -1f;

            if (_aviWriter == null)
            {
                return;
            }

            _aviWriter.Dispose();
            _aviWriter = null;
            Debug.Log($"[WebCamVideoRecording] Recording saved: {NormalizePath(LastSavedFilePath)}");
        }

        private IEnumerator StartCameraRoutine()
        {
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogError("[WebCamVideoRecording] Webcam authorization denied.");
                _cameraStartupRoutine = null;
                yield break;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogError("[WebCamVideoRecording] No webcam devices were found.");
                _cameraStartupRoutine = null;
                yield break;
            }

            int selectedIndex = Mathf.Clamp(_cameraIndex, 0, devices.Length - 1);
            _webCamTexture = new WebCamTexture(
                devices[selectedIndex].name,
                Mathf.Max(16, _requestedWidth),
                Mathf.Max(16, _requestedHeight),
                Mathf.Max(1, _requestedFps));

            _webCamTexture.Play();

            float timeoutAt = Time.realtimeSinceStartup + 8f;
            while (_webCamTexture.width <= 16 && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            if (_webCamTexture.width <= 16 || _webCamTexture.height <= 16)
            {
                Debug.LogError("[WebCamVideoRecording] Webcam started but did not provide valid frame dimensions.");
                CleanupCameraObjects();
                _cameraStartupRoutine = null;
                yield break;
            }

            PrepareFrameBuffers();
            _cameraReady = true;
            _cameraStartupRoutine = null;

            Debug.Log($"[WebCamVideoRecording] Camera ready: {devices[selectedIndex].name} ({_webCamTexture.width}x{_webCamTexture.height}).");

            if (_pendingRecordingStart)
            {
                _pendingRecordingStart = false;
                CreateWriterAndStart();
            }
        }

        private void PrepareFrameBuffers()
        {
            int width = _webCamTexture.width;
            int height = _webCamTexture.height;

            if (_frameTexture == null || _frameTexture.width != width || _frameTexture.height != height)
            {
                if (_frameTexture != null)
                {
                    Destroy(_frameTexture);
                }

                _frameTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            int pixelCount = width * height;
            if (_pixelBuffer == null || _pixelBuffer.Length != pixelCount)
            {
                _pixelBuffer = new Color32[pixelCount];
            }
        }

        private void CreateWriterAndStart()
        {
            int width = _webCamTexture.width;
            int height = _webCamTexture.height;
            int fps = Mathf.Max(1, _recordingFps);

            string directory = Path.Combine(GetOutputBasePath(), _outputFolderName);
            Directory.CreateDirectory(directory);

            string safePrefix = string.IsNullOrWhiteSpace(_fileNamePrefix) ? "webcam" : _fileNamePrefix.Trim();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{safePrefix}_{timestamp}.avi";
            string outputPath = NormalizePath(Path.Combine(directory, fileName));

            _aviWriter = new AviMjpegWriter(outputPath, width, height, fps);
            LastSavedFilePath = outputPath;
            _frameInterval = 1f / fps;
            _nextFrameTime = Time.unscaledTime;
            float recordingDuration = Mathf.Max(0f, _recordingTimeSeconds);
            _recordingStopAtTime = recordingDuration > 0f ? Time.unscaledTime + recordingDuration : -1f;

            Debug.Log($"[WebCamVideoRecording] Recording started: {outputPath}");
        }

        private string GetOutputBasePath()
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                return dataPath;
            }

            return Application.persistentDataPath;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private void CaptureAndWriteFrame()
        {
            _webCamTexture.GetPixels32(_pixelBuffer);
            _frameTexture.SetPixels32(_pixelBuffer);
            _frameTexture.Apply(false, false);

            byte[] jpegBytes = _frameTexture.EncodeToJPG(_jpgQuality);
            _aviWriter.AddFrame(jpegBytes);
        }

        private void CleanupCameraObjects()
        {
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying)
                {
                    _webCamTexture.Stop();
                }

                Destroy(_webCamTexture);
                _webCamTexture = null;
            }

            if (_frameTexture != null)
            {
                Destroy(_frameTexture);
                _frameTexture = null;
            }

            _pixelBuffer = null;
            _cameraReady = false;
        }

        private sealed class AviMjpegWriter : IDisposable
        {
            private const uint AviHasIndexFlag = 0x00000010;
            private const uint KeyFrameFlag = 0x00000010;

            private readonly FileStream _stream;
            private readonly BinaryWriter _writer;
            private readonly List<IndexEntry> _indexEntries = new List<IndexEntry>();
            private readonly int _width;
            private readonly int _height;
            private readonly int _fps;

            private readonly long _riffSizeOffset;
            private readonly long _avihTotalFramesOffset;
            private readonly long _strhLengthOffset;
            private readonly long _moviListSizeOffset;
            private readonly long _moviDataStart;

            private int _frameCount;
            private bool _disposed;

            private struct IndexEntry
            {
                public uint ChunkId;
                public uint Flags;
                public uint Offset;
                public uint Size;
            }

            public AviMjpegWriter(string path, int width, int height, int fps)
            {
                _width = width;
                _height = height;
                _fps = fps;

                _stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                _writer = new BinaryWriter(_stream);

                WriteFourCC("RIFF");
                _riffSizeOffset = _stream.Position;
                _writer.Write(0u);
                WriteFourCC("AVI ");

                long hdrlListSizeOffset = BeginList("hdrl");

                WriteFourCC("avih");
                _writer.Write(56u);
                _writer.Write((uint)(1000000 / _fps));
                _writer.Write((uint)(_width * _height * 3 * _fps));
                _writer.Write(0u);
                _writer.Write(AviHasIndexFlag);
                _avihTotalFramesOffset = _stream.Position;
                _writer.Write(0u);
                _writer.Write(0u);
                _writer.Write(1u);
                _writer.Write((uint)(_width * _height * 3));
                _writer.Write((uint)_width);
                _writer.Write((uint)_height);
                _writer.Write(0u);
                _writer.Write(0u);
                _writer.Write(0u);
                _writer.Write(0u);

                long strlListSizeOffset = BeginList("strl");

                WriteFourCC("strh");
                _writer.Write(56u);
                WriteFourCC("vids");
                WriteFourCC("MJPG");
                _writer.Write(0u);
                _writer.Write((ushort)0);
                _writer.Write((ushort)0);
                _writer.Write(0u);
                _writer.Write(1u);
                _writer.Write((uint)_fps);
                _writer.Write(0u);
                _strhLengthOffset = _stream.Position;
                _writer.Write(0u);
                _writer.Write((uint)(_width * _height * 3));
                _writer.Write(uint.MaxValue);
                _writer.Write(0u);
                _writer.Write((short)0);
                _writer.Write((short)0);
                _writer.Write((short)_width);
                _writer.Write((short)_height);

                WriteFourCC("strf");
                _writer.Write(40u);
                _writer.Write(40u);
                _writer.Write(_width);
                _writer.Write(_height);
                _writer.Write((ushort)1);
                _writer.Write((ushort)24);
                WriteFourCC("MJPG");
                _writer.Write((uint)(_width * _height * 3));
                _writer.Write(0);
                _writer.Write(0);
                _writer.Write(0u);
                _writer.Write(0u);

                EndList(strlListSizeOffset);
                EndList(hdrlListSizeOffset);

                _moviListSizeOffset = BeginList("movi");
                _moviDataStart = _stream.Position;
            }

            public void AddFrame(byte[] jpegFrame)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(AviMjpegWriter));
                }

                if (jpegFrame == null || jpegFrame.Length == 0)
                {
                    return;
                }

                long chunkStart = _stream.Position;
                WriteFourCC("00dc");
                _writer.Write((uint)jpegFrame.Length);
                _writer.Write(jpegFrame);

                if ((jpegFrame.Length & 1) != 0)
                {
                    _writer.Write((byte)0);
                }

                _indexEntries.Add(new IndexEntry
                {
                    ChunkId = FourCcToUInt("00dc"),
                    Flags = KeyFrameFlag,
                    Offset = checked((uint)(chunkStart - _moviDataStart)),
                    Size = (uint)jpegFrame.Length
                });

                _frameCount++;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                long moviEnd = _stream.Position;
                PatchListSize(_moviListSizeOffset, moviEnd);

                WriteFourCC("idx1");
                _writer.Write((uint)(_indexEntries.Count * 16));
                for (int i = 0; i < _indexEntries.Count; i++)
                {
                    IndexEntry entry = _indexEntries[i];
                    _writer.Write(entry.ChunkId);
                    _writer.Write(entry.Flags);
                    _writer.Write(entry.Offset);
                    _writer.Write(entry.Size);
                }

                long endOfFile = _stream.Position;

                PatchUInt(_avihTotalFramesOffset, (uint)_frameCount);
                PatchUInt(_strhLengthOffset, (uint)_frameCount);
                PatchUInt(_riffSizeOffset, checked((uint)(endOfFile - 8)));

                _writer.Flush();
                _writer.Dispose();
                _disposed = true;
            }

            private long BeginList(string listType)
            {
                WriteFourCC("LIST");
                long sizeOffset = _stream.Position;
                _writer.Write(0u);
                WriteFourCC(listType);
                return sizeOffset;
            }

            private void EndList(long listSizeOffset)
            {
                PatchListSize(listSizeOffset, _stream.Position);
            }

            private void PatchListSize(long listSizeOffset, long listEndPosition)
            {
                uint listSize = checked((uint)(listEndPosition - (listSizeOffset + 4)));
                PatchUInt(listSizeOffset, listSize);
            }

            private void PatchUInt(long position, uint value)
            {
                long current = _stream.Position;
                _stream.Position = position;
                _writer.Write(value);
                _stream.Position = current;
            }

            private void WriteFourCC(string fourCC)
            {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(fourCC);
                if (bytes.Length != 4)
                {
                    throw new ArgumentException("FourCC must be exactly 4 characters.", nameof(fourCC));
                }

                _writer.Write(bytes);
            }

            private static uint FourCcToUInt(string fourCC)
            {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(fourCC);
                if (bytes.Length != 4)
                {
                    throw new ArgumentException("FourCC must be exactly 4 characters.", nameof(fourCC));
                }

                return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
            }
        }
    }
}
