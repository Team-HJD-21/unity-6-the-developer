// 실제 회사 영상의 디코딩·화면 프레임과 종료 후 기존 Initial_Stage 조립을 확인합니다.
// GPU가 있는 Editor 실행을 사용하며 기존 Scene 직접 Play 검사를 변경하지 않습니다.
using System.Collections;
using System.IO;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CompanySplashIntegrationTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (UnityEngine.Application.isPlaying)
            {
                Object.FindAnyObjectByType<AppRoot>()?.EndCurrentMatch();
                yield return new ExitPlayMode();
            }
        }
        [UnityTest]
        public IEnumerator ActualCompanyMovieRendersThenEntersInitialStage()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CompanySplash.unity");
            yield return new EnterPlayMode();
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            VideoPlayer video;
            do
            {
                video = Object.FindAnyObjectByType<VideoPlayer>();
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "회사 영상의 실제 프레임 준비 시간 초과");
                yield return null;
            } while (video == null || !video.isPlaying || video.time < video.length * .5);
            Assert.That(video.audioOutputMode, Is.EqualTo(VideoAudioOutputMode.None));
            Assert.That(video.targetTexture, Is.Not.Null);
            yield return null;
            var frame = new Texture2D(video.targetTexture.width, video.targetTexture.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = video.targetTexture;
                frame.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); frame.Apply();
                bool hasLogo = false;
                foreach (Color32 pixel in frame.GetPixels32())
                    if (pixel.r > 20 || pixel.g > 20 || pixel.b > 20) { hasLogo = true; break; }
                Assert.That(hasLogo, Is.True, "실제 디코딩 프레임이 검은 화면뿐입니다.");
                string folder = "Docs/Local/Validation/CompanySplash20261028";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "company-splash-frame.png"), frame.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(frame); }
            deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (SceneManager.GetActiveScene().path != "Assets/Scenes/Gameplay/Initial_Stage.unity" ||
                Object.FindAnyObjectByType<AppRoot>().CurrentMatchId.IsEmpty)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "영상 후 기존 게임 진입 시간 초과");
                yield return null;
            }
            Assert.That(Object.FindAnyObjectByType<VideoPlayer>(), Is.Null, "스플래시 재생 자원이 남았습니다.");
            Debug.Log("COMPANY_SPLASH_VERIFIED: real movie frame → playback end → Initial_Stage Host Match; video released.");
            yield return new ExitPlayMode();
        }
    }
}
