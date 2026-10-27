// 회사 로고 영상을 무음·원본 비율로 표시하고 재생 자원을 Scene 수명에 맞춰 정리합니다.
// 준비/재생 실패는 기록한 뒤 종료하여 게임 진입이 영구 대기하지 않게 합니다.
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TeamHJD.Game.Presentation
{
    public sealed class CompanySplashPlayer : MonoBehaviour
    {
        private VideoPlayer _video;
        private RenderTexture _texture;
        private RawImage _image;
        private GameObject _canvas, _camera;
        private bool _finished;
        private string _failure;

        public IEnumerator Play(string relativePath)
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, relativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning("Company splash video is missing; continuing startup.", this);
                yield break;
            }
            if (!PrepareView(path)) yield break;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                _video.Prepare();
                while (!_video.isPrepared && _failure == null && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                if (!_video.isPrepared && _failure == null) _failure = "Video preparation timed out.";
                if (_failure == null)
                {
                    _video.Play();
                    deadline = Time.realtimeSinceStartupAsDouble + Math.Max(15, _video.length + 10);
                    while (!_finished && _failure == null && Time.realtimeSinceStartupAsDouble < deadline)
                        yield return null;
                    if (!_finished && _failure == null) _failure = "Video playback timed out.";
                }
                if (_failure != null) Debug.LogWarning("Company splash: " + _failure + " Continuing startup.", this);
            }
            finally { ReleaseVideo(); }
        }

        private bool PrepareView(string path)
        {
            try
            {
                _finished = false; _failure = null;
                _camera = new GameObject("Splash Camera", typeof(Camera));
                _camera.transform.SetParent(transform, false);
                var camera = _camera.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.cullingMask = 0;
                _canvas = new GameObject("Company Splash", typeof(RectTransform), typeof(Canvas), typeof(Image));
                _canvas.transform.SetParent(transform, false);
                _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.GetComponent<Canvas>().sortingOrder = 32760;
                _canvas.GetComponent<Image>().color = Color.black;
                var picture = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                picture.transform.SetParent(_canvas.transform, false);
                var ratio = picture.GetComponent<AspectRatioFitter>();
                ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                ratio.aspectRatio = 16f / 9;
                _image = picture.GetComponent<RawImage>();
                _image.raycastTarget = false;
                _texture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                _texture.Create();
                var previous = RenderTexture.active;
                try { RenderTexture.active = _texture; GL.Clear(true, true, Color.black); }
                finally { RenderTexture.active = previous; }
                _image.texture = _texture;
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.playOnAwake = false; _video.isLooping = false;
                _video.source = VideoSource.Url; _video.url = new Uri(path).AbsoluteUri;
                _video.renderMode = VideoRenderMode.RenderTexture; _video.targetTexture = _texture;
                _video.audioOutputMode = VideoAudioOutputMode.None;
                _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                _video.waitForFirstFrame = true;
                _video.loopPointReached += OnFinished;
                _video.errorReceived += OnError;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Company splash setup failed: " + error.Message, this);
                ReleaseVideo(); return false;
            }
        }
        private void OnFinished(VideoPlayer _) => _finished = true;
        private void OnError(VideoPlayer _, string message) => _failure = message;
        private void ReleaseVideo()
        {
            if (_image != null) { _image.texture = null; _image.color = Color.black; }
            if (_video != null)
            {
                _video.loopPointReached -= OnFinished; _video.errorReceived -= OnError;
                _video.Stop(); _video.targetTexture = null;
                Destroy(_video); _video = null;
            }
            if (_texture != null) { _texture.Release(); Destroy(_texture); _texture = null; }
        }
        private void OnDestroy()
        {
            ReleaseVideo();
            if (_canvas != null) Destroy(_canvas);
            if (_camera != null) Destroy(_camera);
        }
    }
}
