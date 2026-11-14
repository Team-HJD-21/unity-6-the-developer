// Core의 기본 최종 Scene 조립부입니다. 실제 참가자를 배치하고 Enemy 보스 원본·선택적 연출을 준비합니다.
// 저장/NGO Scene 로드·실패 참가자 원복은 Root가 맡고, 이 클래스는 자기 보스/연출/카메라만 정리합니다.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    [DisallowMultipleComponent]
    public sealed class FinalBattleSceneDriver : MonoBehaviour, IFinalBattleSceneDriver
    {
        [SerializeField, Tooltip("Build Settings의 실제 최종 Boss Scene 전체 경로입니다.")]
        private string scenePath;
        private IFinalBattleBossSource _boss;
        private PlayableDirector _intro;
        private DirectorUpdateMode _originalUpdateMode;
        private IDisposable _view;
        private bool _prepared;
        public string ScenePath => scenePath;

        /// <summary>Root만 저장/로드 완료 뒤 호출합니다. 실제 도착점·보스 준비와 선택적 Timeline 종료까지 기다립니다.</summary>
        public async Task<FinalBattleSceneReady> PrepareAsync(InitialStageCompositionRoot stage, CancellationToken cancellation)
        {
            if (stage == null || !stage.CanPrepareFinalBossScene || _prepared ||
                stage.MatchCoordinator?.IsGameplayRunning != true)
                throw new InvalidOperationException("저장 완료된 현재 Host의 최종 진입 범위가 필요합니다.");
            cancellation.ThrowIfCancellationRequested();
            var destination = SceneManager.GetSceneByPath(scenePath);
            if (!destination.IsValid() || !destination.isLoaded || destination == stage.gameObject.scene)
                throw new InvalidOperationException("실제로 로드된 별도 Boss Scene이 필요합니다.");
            var layouts = destination.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FinalBattleSceneLayout>(true)).ToArray();
            if (layouts.Length != 1 || !layouts[0].isActiveAndEnabled)
                throw new InvalidOperationException("Boss Scene에는 활성 FinalBattleSceneLayout 하나가 필요합니다.");
            var layout = layouts[0];
            var actors = stage.AuthorityActors.OrderBy(p => p.Key.Value, StringComparer.Ordinal).ToArray();
            if (actors.Length == 0 || layout.playerStarts == null || layout.playerStarts.Length < actors.Length ||
                actors.Any(p => p.Value == null) || layout.playerStarts.Take(actors.Length).Any(p =>
                    p == null || !p.gameObject.activeInHierarchy || p.gameObject.scene != destination))
                throw new InvalidOperationException("모든 실제 참가자의 동일 Scene 도착점을 지정해야 합니다.");
            if (layout.bossSource == null || !layout.bossSource.isActiveAndEnabled ||
                layout.bossSource.gameObject.scene != destination || !(layout.bossSource is IFinalBattleBossSource source))
                throw new InvalidOperationException("같은 Boss Scene의 실제 IFinalBattleBossSource가 필요합니다.");
            if (!float.IsFinite(layout.cameraBlendSeconds) || layout.cameraBlendSeconds < 0 ||
                (layout.intro != null && layout.intro.gameObject.scene != destination) ||
                (layout.introView != null && layout.introView.gameObject.scene != destination))
                throw new InvalidOperationException("연출·시점은 같은 Boss Scene과 유효한 배속/블렌딩 설정을 사용해야 합니다.");
            // NGO Scene 이동을 따르지 않는 원격 개체를 도착 완료라고 보고하지 않습니다.
            foreach (var pair in actors)
                if (pair.Value.TryGetComponent<NetworkObject>(out var network) && network.IsSpawned && !network.SceneMigrationSynchronization)
                    throw new InvalidOperationException("참가자 NetworkObject의 SceneMigrationSynchronization이 필요합니다.");

            _prepared = true;
            _boss = source;
            try
            {
                for (int index = 0; index < actors.Length; index++)
                {
                    var actor = actors[index].Value;
                    actor.transform.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(actor, destination);
                    actor.transform.SetPositionAndRotation(layout.playerStarts[index].position, layout.playerStarts[index].rotation);
                    if (actor.TryGetComponent<Rigidbody2D>(out var body))
                    { body.position = actor.transform.position; body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
                }
                Physics2D.SyncTransforms();
                var identity = source.PrepareFinalBoss(stage.MatchTime, stage.MatchCoordinator.GameplayCancellation);
                if (identity.IsEmpty) throw new InvalidOperationException("실제 보스 생성 ID가 없습니다.");
                SnapLocalCamera();
                if (layout.intro != null)
                {
                    _intro = layout.intro;
                    _originalUpdateMode = _intro.timeUpdateMode;
                    if (!double.IsFinite(_intro.duration) || _intro.duration <= 0 || _intro.extrapolationMode == DirectorWrapMode.Loop)
                        throw new InvalidOperationException("도입 Timeline은 유한한 길이의 단발 연출이어야 합니다.");
                    if (layout.introView != null)
                    {
                        if (CameraManager.Current == null) throw new InvalidOperationException("연출을 표시할 현재 로컬 Camera가 없습니다.");
                        _view = CameraManager.Current.PushView(layout.introView, layout.cameraBlendSeconds);
                    }
                    _intro.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                    _intro.time = 0;
                    _intro.Play();
                    while (_intro != null && _intro.state == PlayState.Playing)
                    { cancellation.ThrowIfCancellationRequested(); await Task.Yield(); }
                }
                cancellation.ThrowIfCancellationRequested();
                ReleasePresentation();
                return new FinalBattleSceneReady(stage.MatchTime.Snapshot.MatchId, identity, actors.Select(p => p.Key));
            }
            catch { StopMatch(); throw; }
        }

        /// <summary>Core가 참가자를 Field로 원복하기 전에 소유한 Enemy/연출만 정리합니다.</summary>
        public Task RollbackAsync(CancellationToken cancellation)
        { StopMatch(); cancellation.ThrowIfCancellationRequested(); return Task.CompletedTask; }

        /// <summary>Match 종료/실패에서 반복 호출할 수 있습니다. Scene 해제·참가자 이동은 Root만 수행합니다.</summary>
        public void StopMatch()
        {
            try { ReleasePresentation(); }
            finally
            {
                var boss = _boss;
                _boss = null; _prepared = false;
                boss?.ReleaseFinalBoss();
            }
        }

        private void ReleasePresentation()
        {
            try
            {
                if (_intro != null) { _intro.Stop(); _intro.timeUpdateMode = _originalUpdateMode; }
            }
            finally { _intro = null; _view?.Dispose(); _view = null; SnapLocalCamera(); }
        }

        private static void SnapLocalCamera()
        {
            var output = CameraManager.Current?.OutputCamera;
            if (output != null && output.TryGetComponent<MatchCameraFollow>(out var follow)) follow.SnapToTarget();
        }
    }
}
