// 실제 Initial_Stage의 Play 진입·동적 모드·CU 시작 셀과 writer 등록을 확인합니다.
// 제품 Scene을 저장하지 않으며 실패한 검사도 Play 종료로 정리합니다.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class DynamicRegionRuntimeTests
    {
        [UnityTest, Explicit("실제 Stage의 CU·터렛 조립과 시작 셀 설정을 확인합니다.")]
        public IEnumerator InitialStageUsesCuSeedAndRegistersItsCheckpointWriter()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Assert.That(scene.isDirty, Is.False, "미저장 Scene은 검사하지 않습니다.");
            // Test Runner는 빈 Scene에서 시작하므로 실제 검사 대상을 명시적으로 엽니다.
            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 25;
            RegionSceneBinding binding;
            do
            {
                binding = Object.FindObjectsByType<RegionSceneBinding>(FindObjectsSortMode.None)
                    .SingleOrDefault(item => item.HasWriteAuthority && item.Source != null);
                if (binding != null) break;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            Assert.That(binding, Is.Not.Null, "실제 Match 지역 바인딩이 준비되지 않았습니다.");
            Assert.That(binding.ResetPolicy(RegionPolicyMode.Dynamic, true), Is.True);
            var definition = binding.Source.Snapshot.Definition;
            Vector3 cu = binding.ControlUnit.position;
            Assert.That(definition.TryGetCellIndex(new BattlefieldPoint(cu.x, cu.y), out int start), Is.True);
            Assert.That(binding.Source.Snapshot.CapturedCells[start], Is.True);
            Assert.That(binding.LastEvaluation.InitialCellCount, Is.EqualTo(1));
            Assert.That(binding.LastEvaluation.Geometry.Vertices.Any(v => v.EntityId.Value == DynamicRegionCapturePolicy.ControlUnitNodeId), Is.True);
            int width = definition.Geometry.CellsX;
            int neighbor = start % width + 1 < width ? start + 1 : start - 1;
            Assert.That(binding.TrySetDynamicStartCells(new[] { start, neighbor }, true, out string error), Is.True, error);
            yield return null;
            Assert.That(binding.LastEvaluation.InitialCellCount, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { start, neighbor }, binding.CaptureDynamicStartCells());
            Guid current = binding.Source.Snapshot.GridId;
            Assert.That(binding.TrySetDynamicStartCells(new[] { definition.Geometry.TotalCellCount }, true, out _), Is.False);
            Assert.That(binding.Source.Snapshot.GridId, Is.EqualTo(current));
            var app = Object.FindAnyObjectByType<AppRoot>();
            Assert.That(app, Is.Not.Null);
            var root = Object.FindObjectsByType<InitialStageCompositionRoot>(FindObjectsSortMode.None).Single();
            Assert.That(root.Checkpoints, Is.Not.Null);
            Assert.That(root.MissingCheckpointParticipants, Does.Not.Contain("world/region-start"));
            Assert.That(((ICheckpointParticipant)binding).Key, Is.EqualTo("world/region-start"));
            Assert.That(binding.TrySetDynamicStartCells(null, true, out error), Is.True, error);
            Assert.That(binding.LastEvaluation.InitialCellCount, Is.EqualTo(1));
            Assert.That(binding.LastDynamicError, Is.Null);
            yield return VerifyRegionControls();
            yield return VerifyFadeCancellation();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyRegionControls()
        {
            FieldInfo expanded = typeof(RegionDebugControls).GetField("_showStartCells", BindingFlags.NonPublic | BindingFlags.Static);
            bool before = (bool)expanded.GetValue(null);
            var window = ScriptableObject.CreateInstance<RegionControlsSmokeWindow>();
            try
            {
                expanded.SetValue(null, true);
                window.titleContent = new GUIContent("Core region QA");
                window.position = new Rect(80, 80, 600, 650);
                window.ShowUtility();
                float deadline = Time.realtimeSinceStartup + 3;
                while (window.DrawCount == 0 && window.DrawFailure == null && Time.realtimeSinceStartup < deadline)
                {
                    window.Repaint();
                    yield return null;
                }
                Assert.That(window.DrawFailure, Is.Null);
                Assert.That(window.DrawCount, Is.GreaterThan(0), "시작 셀 Editor UI가 실제로 그려지지 않았습니다.");
            }
            finally
            {
                expanded.SetValue(null, before);
                window.Close();
            }
        }

        private static IEnumerator VerifyFadeCancellation()
        {
            var parent = new GameObject("Core fade cancellation QA");
            try
            {
                using var fade = new SceneFadePresenter(parent.transform);
                using var cancellation = new CancellationTokenSource();
                int moves = 0;
                Task task = fade.RunAsync(() => { moves++; return Task.CompletedTask; }, cancellation.Token);
                cancellation.Cancel();
                float deadline = Time.realtimeSinceStartup + 3;
                while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(task.IsCanceled, Is.True, "명시적인 호출자 취소는 정상적으로 전파돼야 합니다.");
                Assert.That(moves, Is.Zero);
                Assert.That(fade.IsVisible, Is.False);
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTearDown]
        public IEnumerator ExitPlayWhenAssertionFails()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        private sealed class RegionControlsSmokeWindow : EditorWindow
        {
            public int DrawCount { get; private set; }
            public Exception DrawFailure { get; private set; }
            private void OnGUI()
            {
                try { RegionDebugControls.Draw(); DrawCount++; }
                catch (Exception error) { DrawFailure = error; }
            }
        }
    }
}
