// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace Mediapipe.Unity
{
  public class VideoSource : ImageSource
  {
    private readonly VideoClip[] _builtInSources;
    private readonly GameObject _gameObject;
    private readonly List<VideoSourceEntry> _sourceEntries = new();

    private string[] _externalSourcePaths = Array.Empty<string>();
    private bool _useBuiltInSources = true;
    private int _selectedSourceIndex = -1;
    private VideoPlayer _videoPlayer;
    private bool _loop = true;

    private MjpegAviPlayback _mjpegPlayback;
    private Texture2D _mjpegTexture;
    private bool _useMjpegPlayback;
    private bool _isMjpegPrepared;
    private bool _isMjpegPlaying;
    private int _mjpegCurrentFrameIndex = -1;
    private float _mjpegStartTime;
    private float _mjpegElapsedBeforePause;

    private struct VideoSourceEntry
    {
      public string name;
      public VideoClip clip;
      public string url;

      public VideoSourceEntry(VideoClip sourceClip)
      {
        name = sourceClip != null ? sourceClip.name : string.Empty;
        clip = sourceClip;
        url = null;
      }

      public VideoSourceEntry(string sourcePath)
      {
        name = Path.GetFileName(sourcePath);
        clip = null;
        url = sourcePath;
      }
    }

    public VideoSource(VideoClip[] availableSources)
    {
      _builtInSources = availableSources ?? Array.Empty<VideoClip>();
      _gameObject = new GameObject("Video Player");
      RebuildSourceEntries();
    }

    public bool loop
    {
      get => _loop;
      set
      {
        _loop = value;

        if (_videoPlayer != null)
        {
          _videoPlayer.isLooping = _loop;
        }
      }
    }

    public override string sourceName
    {
      get
      {
        if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sourceEntries.Count)
        {
          return null;
        }

        return _sourceEntries[_selectedSourceIndex].name;
      }
    }

    public override string[] sourceCandidateNames => _sourceEntries.Select(source => source.name).ToArray();

    public override ResolutionStruct[] availableResolutions
    {
      get
      {
        if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sourceEntries.Count)
        {
          return null;
        }

        VideoSourceEntry selectedSource = _sourceEntries[_selectedSourceIndex];
        if (selectedSource.clip != null)
        {
          return new[] { new ResolutionStruct((int)selectedSource.clip.width, (int)selectedSource.clip.height, selectedSource.clip.frameRate) };
        }

        if (_useMjpegPlayback)
        {
          return _isMjpegPrepared ? new[] { resolution } : null;
        }

        if (isPrepared)
        {
          return new[] { resolution };
        }

        return null;
      }
    }

    public override bool isPlaying => _useMjpegPlayback ? _isMjpegPlaying : (_videoPlayer != null && _videoPlayer.isPlaying);
    public override bool isPrepared => _useMjpegPlayback ? _isMjpegPrepared : (_videoPlayer != null && _videoPlayer.isPrepared);

    public void SetExternalSourcePaths(string[] sourcePaths, bool useOnlyExternalSources = false)
    {
      _externalSourcePaths = (sourcePaths ?? Array.Empty<string>())
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.GetFullPath(path))
        .Where(path => File.Exists(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

      _useBuiltInSources = !useOnlyExternalSources;
      RebuildSourceEntries();
    }

    public override void SelectSource(int sourceId)
    {
      if (sourceId < 0 || sourceId >= _sourceEntries.Count)
      {
        throw new ArgumentException($"Invalid source ID: {sourceId}");
      }

      _selectedSourceIndex = sourceId;
      UpdateResolutionFromSelectedSource();

      if (_videoPlayer != null)
      {
        ApplySelectedSource(_videoPlayer);
      }
    }

    public override IEnumerator Play()
    {
      if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sourceEntries.Count)
      {
        throw new InvalidOperationException("Video is not selected");
      }

      Stop();
      VideoSourceEntry selectedSource = _sourceEntries[_selectedSourceIndex];

      if (ShouldUseMjpegPlayback(selectedSource))
      {
        InitializeMjpegPlayback(selectedSource.url);
        yield break;
      }

      _videoPlayer = _gameObject.AddComponent<VideoPlayer>();
      _videoPlayer.renderMode = VideoRenderMode.APIOnly;
      _videoPlayer.isLooping = _loop;
      ApplySelectedSource(_videoPlayer);
      _videoPlayer.Prepare();

      const float prepareTimeoutSeconds = 10f;
      float timeoutAt = Time.realtimeSinceStartup + prepareTimeoutSeconds;
      yield return new WaitUntil(() =>
        _videoPlayer == null ||
        _videoPlayer.isPrepared ||
        Time.realtimeSinceStartup >= timeoutAt);

      if (_videoPlayer == null || !_videoPlayer.isPrepared)
      {
        throw new TimeoutException($"Failed to prepare video source: {sourceName}");
      }

      UpdateResolutionFromPreparedPlayer();
      _videoPlayer.Play();
    }

    public override IEnumerator Resume()
    {
      if (_useMjpegPlayback)
      {
        if (!_isMjpegPrepared)
        {
          throw new InvalidOperationException("MJPEG video is not prepared");
        }

        if (!_isMjpegPlaying)
        {
          _isMjpegPlaying = true;
          _mjpegStartTime = Time.unscaledTime - _mjpegElapsedBeforePause;
        }

        yield break;
      }

      if (!isPrepared)
      {
        throw new InvalidOperationException("VideoPlayer is not prepared");
      }

      if (!isPlaying)
      {
        _videoPlayer.Play();
      }

      yield return null;
    }

    public override void Pause()
    {
      if (_useMjpegPlayback)
      {
        if (!_isMjpegPlaying)
        {
          return;
        }

        _mjpegElapsedBeforePause = Mathf.Max(0f, Time.unscaledTime - _mjpegStartTime);
        _isMjpegPlaying = false;
        return;
      }

      if (!isPlaying)
      {
        return;
      }

      _videoPlayer.Pause();
    }

    public override void Stop()
    {
      if (_videoPlayer != null)
      {
        _videoPlayer.Stop();
        UnityEngine.Object.Destroy(_videoPlayer);
        _videoPlayer = null;
      }

      if (_mjpegPlayback != null)
      {
        _mjpegPlayback.Dispose();
        _mjpegPlayback = null;
      }

      if (_mjpegTexture != null)
      {
        UnityEngine.Object.Destroy(_mjpegTexture);
        _mjpegTexture = null;
      }

      _useMjpegPlayback = false;
      _isMjpegPrepared = false;
      _isMjpegPlaying = false;
      _mjpegCurrentFrameIndex = -1;
      _mjpegStartTime = 0f;
      _mjpegElapsedBeforePause = 0f;
    }

    public override Texture GetCurrentTexture()
    {
      if (_useMjpegPlayback)
      {
        UpdateMjpegFrame();
        return _mjpegTexture;
      }

      return _videoPlayer != null ? _videoPlayer.texture : null;
    }

    private void RebuildSourceEntries()
    {
      _sourceEntries.Clear();

      if (_useBuiltInSources)
      {
        for (int i = 0; i < _builtInSources.Length; i++)
        {
          VideoClip clip = _builtInSources[i];
          if (clip != null)
          {
            _sourceEntries.Add(new VideoSourceEntry(clip));
          }
        }
      }

      for (int i = 0; i < _externalSourcePaths.Length; i++)
      {
        _sourceEntries.Add(new VideoSourceEntry(_externalSourcePaths[i]));
      }

      if (_sourceEntries.Count == 0)
      {
        _selectedSourceIndex = -1;
        return;
      }

      if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sourceEntries.Count)
      {
        _selectedSourceIndex = 0;
      }

      UpdateResolutionFromSelectedSource();
    }

    private void ApplySelectedSource(VideoPlayer player)
    {
      if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sourceEntries.Count)
      {
        throw new InvalidOperationException("Video source is not selected");
      }

      VideoSourceEntry source = _sourceEntries[_selectedSourceIndex];
      if (source.clip != null)
      {
        player.source = UnityEngine.Video.VideoSource.VideoClip;
        player.clip = source.clip;
        player.url = string.Empty;
      }
      else
      {
        player.source = UnityEngine.Video.VideoSource.Url;
        player.clip = null;
        player.url = new Uri(source.url).AbsoluteUri;
      }
    }

    private void UpdateResolutionFromSelectedSource()
    {
      if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sourceEntries.Count)
      {
        return;
      }

      VideoSourceEntry source = _sourceEntries[_selectedSourceIndex];
      if (source.clip != null)
      {
        resolution = new ResolutionStruct((int)source.clip.width, (int)source.clip.height, source.clip.frameRate);
      }
    }

    private void UpdateResolutionFromPreparedPlayer()
    {
      if (_videoPlayer == null)
      {
        return;
      }

      int width = (int)_videoPlayer.width;
      int height = (int)_videoPlayer.height;

      if (width <= 0 || height <= 0)
      {
        Texture currentTexture = _videoPlayer.texture;
        if (currentTexture != null)
        {
          width = currentTexture.width;
          height = currentTexture.height;
        }
      }

      if (width <= 0 || height <= 0)
      {
        UpdateResolutionFromSelectedSource();
        return;
      }

      resolution = new ResolutionStruct(width, height, _videoPlayer.frameRate);
    }

    private bool ShouldUseMjpegPlayback(VideoSourceEntry source)
    {
      if (source.clip != null || string.IsNullOrWhiteSpace(source.url))
      {
        return false;
      }

      return string.Equals(Path.GetExtension(source.url), ".avi", StringComparison.OrdinalIgnoreCase);
    }

    private void InitializeMjpegPlayback(string path)
    {
      _mjpegPlayback = MjpegAviPlayback.Open(path);

      _useMjpegPlayback = true;
      _isMjpegPrepared = true;
      _isMjpegPlaying = true;
      _mjpegCurrentFrameIndex = -1;
      _mjpegElapsedBeforePause = 0f;
      _mjpegStartTime = Time.unscaledTime;

      _mjpegTexture = new Texture2D(
        Mathf.Max(1, _mjpegPlayback.Width),
        Mathf.Max(1, _mjpegPlayback.Height),
        TextureFormat.RGB24,
        false);

      resolution = new ResolutionStruct(_mjpegPlayback.Width, _mjpegPlayback.Height, _mjpegPlayback.FrameRate);
      UpdateMjpegFrame(forceUpdate: true);

      Debug.Log($"[VideoSource] Using MJPEG AVI fallback for {Path.GetFileName(path)} ({_mjpegPlayback.Width}x{_mjpegPlayback.Height} @ {_mjpegPlayback.FrameRate:0.##}fps)");
    }

    private void UpdateMjpegFrame(bool forceUpdate = false)
    {
      if (!_useMjpegPlayback || !_isMjpegPrepared || _mjpegPlayback == null || _mjpegTexture == null)
      {
        return;
      }

      if (_mjpegPlayback.FrameCount <= 0)
      {
        return;
      }

      float elapsedSeconds = _isMjpegPlaying
        ? Mathf.Max(0f, Time.unscaledTime - _mjpegStartTime)
        : _mjpegElapsedBeforePause;

      if (!_loop && elapsedSeconds >= _mjpegPlayback.DurationSeconds)
      {
        elapsedSeconds = _mjpegPlayback.DurationSeconds;
        _mjpegElapsedBeforePause = elapsedSeconds;
        _isMjpegPlaying = false;
      }

      int frameIndex = _mjpegPlayback.GetFrameIndex(elapsedSeconds, _loop);
      if (!forceUpdate && frameIndex == _mjpegCurrentFrameIndex)
      {
        return;
      }

      byte[] jpegFrame = _mjpegPlayback.ReadFrame(frameIndex);
      if (jpegFrame == null || jpegFrame.Length == 0)
      {
        return;
      }

      _mjpegTexture.LoadImage(jpegFrame, false);
      _mjpegCurrentFrameIndex = frameIndex;
    }

    private sealed class MjpegAviPlayback : IDisposable
    {
      private static readonly uint FourCCRiff = ToFourCC("RIFF");
      private static readonly uint FourCCAvi = ToFourCC("AVI ");
      private static readonly uint FourCCList = ToFourCC("LIST");
      private static readonly uint FourCCAvih = ToFourCC("avih");
      private static readonly uint FourCC00dc = ToFourCC("00dc");
      private static readonly uint FourCCMovi = ToFourCC("movi");

      private readonly FileStream _stream;
      private readonly List<FrameInfo> _frames;

      private struct FrameInfo
      {
        public long offset;
        public int size;
      }

      public int Width { get; }
      public int Height { get; }
      public float FrameRate { get; }
      public int FrameCount => _frames.Count;
      public float DurationSeconds => FrameRate > 0f ? FrameCount / FrameRate : 0f;

      private MjpegAviPlayback(FileStream stream, int width, int height, float frameRate, List<FrameInfo> frames)
      {
        _stream = stream;
        _frames = frames;
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        FrameRate = frameRate > 0f ? frameRate : 30f;
      }

      public static MjpegAviPlayback Open(string path)
      {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
          throw new FileNotFoundException("AVI file not found", path);
        }

        FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true);

        uint riff = reader.ReadUInt32();
        uint riffSize = reader.ReadUInt32();
        uint avi = reader.ReadUInt32();

        if (riff != FourCCRiff || avi != FourCCAvi)
        {
          reader.Dispose();
          stream.Dispose();
          throw new InvalidDataException("File is not a valid AVI stream.");
        }

        long riffEnd = Math.Min(stream.Length, 8L + riffSize);
        int width = 0;
        int height = 0;
        float frameRate = 30f;
        List<FrameInfo> frames = new List<FrameInfo>();

        ParseChunkRange(reader, riffEnd, false, frames, ref width, ref height, ref frameRate);

        if (frames.Count == 0)
        {
          reader.Dispose();
          stream.Dispose();
          throw new InvalidDataException("AVI contains no MJPEG frames.");
        }

        reader.Dispose();
        stream.Position = 0;
        return new MjpegAviPlayback(stream, width, height, frameRate, frames);
      }

      public int GetFrameIndex(float elapsedSeconds, bool loop)
      {
        if (FrameCount <= 0)
        {
          return 0;
        }

        if (FrameRate <= 0f)
        {
          return 0;
        }

        if (!loop)
        {
          int clamped = Mathf.Clamp(Mathf.FloorToInt(elapsedSeconds * FrameRate), 0, FrameCount - 1);
          return clamped;
        }

        float duration = DurationSeconds;
        if (duration <= 0f)
        {
          return 0;
        }

        float wrapped = elapsedSeconds % duration;
        if (wrapped < 0f)
        {
          wrapped += duration;
        }

        int index = Mathf.Clamp(Mathf.FloorToInt(wrapped * FrameRate), 0, FrameCount - 1);
        return index;
      }

      public byte[] ReadFrame(int frameIndex)
      {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
        {
          throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        FrameInfo frameInfo = _frames[frameIndex];
        byte[] bytes = new byte[frameInfo.size];

        _stream.Position = frameInfo.offset;
        int bytesRead = 0;
        while (bytesRead < frameInfo.size)
        {
          int read = _stream.Read(bytes, bytesRead, frameInfo.size - bytesRead);
          if (read <= 0)
          {
            break;
          }

          bytesRead += read;
        }

        if (bytesRead != frameInfo.size)
        {
          throw new EndOfStreamException("Could not read full MJPEG frame from AVI file.");
        }

        return bytes;
      }

      public void Dispose()
      {
        _stream?.Dispose();
      }

      private static void ParseChunkRange(
        BinaryReader reader,
        long rangeEnd,
        bool inMoviList,
        List<FrameInfo> frames,
        ref int width,
        ref int height,
        ref float frameRate)
      {
        while (reader.BaseStream.Position + 8 <= rangeEnd)
        {
          uint chunkId = reader.ReadUInt32();
          uint chunkSize = reader.ReadUInt32();
          long chunkDataStart = reader.BaseStream.Position;
          long nextChunkPos = chunkDataStart + chunkSize + (chunkSize % 2);

          if (chunkId == FourCCList)
          {
            if (chunkSize < 4 || reader.BaseStream.Position + 4 > rangeEnd)
            {
              reader.BaseStream.Position = Math.Min(nextChunkPos, rangeEnd);
              continue;
            }

            uint listType = reader.ReadUInt32();
            long listEnd = Math.Min(chunkDataStart + chunkSize, rangeEnd);
            bool recurseInMovi = inMoviList || listType == FourCCMovi;
            ParseChunkRange(reader, listEnd, recurseInMovi, frames, ref width, ref height, ref frameRate);
          }
          else if (chunkId == FourCCAvih)
          {
            ParseAvih(reader, chunkDataStart, chunkSize, ref width, ref height, ref frameRate);
          }
          else if (inMoviList && chunkId == FourCC00dc)
          {
            if (chunkSize > 0 && chunkDataStart + chunkSize <= reader.BaseStream.Length)
            {
              frames.Add(new FrameInfo
              {
                offset = chunkDataStart,
                size = (int)chunkSize
              });
            }
          }

          reader.BaseStream.Position = Math.Min(nextChunkPos, rangeEnd);
        }
      }

      private static void ParseAvih(BinaryReader reader, long chunkDataStart, uint chunkSize, ref int width, ref int height, ref float frameRate)
      {
        if (chunkSize < 40)
        {
          return;
        }

        reader.BaseStream.Position = chunkDataStart;
        uint microsecPerFrame = reader.ReadUInt32();
        if (microsecPerFrame > 0)
        {
          frameRate = 1000000f / microsecPerFrame;
        }

        reader.BaseStream.Position = chunkDataStart + 32;
        width = reader.ReadInt32();
        height = reader.ReadInt32();
      }

      private static uint ToFourCC(string value)
      {
        if (string.IsNullOrEmpty(value) || value.Length != 4)
        {
          throw new ArgumentException("FourCC must be exactly four characters.", nameof(value));
        }

        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
        return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
      }
    }
  }
}
