// 시작 우주선의 출격과 진행 중 Match의 임시 우주선 방문을 구분합니다.
// 0.3초 암전·최소 0.3초 이동·0.3초 복귀 안에서 actor·카메라를 맞추고 위치를 보고합니다.
// 방문 actor는 필드 Scene 소유로 유지하며, Match를 종료하거나 새로 생성하지 않습니다.
using System;
using System.IO;
using System.Threading.Tasks;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Bootstrap
{
    internal sealed class SpaceshipTravelService : ISpaceshipTravel
    {
        private readonly AppRoot _app;
        private readonly UnitySceneFlow _localScenes = new();
        private Transform _visitor;
        private Vector3 _returnPosition;
        private string _fieldScene;
        private string _spaceshipScene;
        public bool IsTransitioning { get; private set; }
        internal bool IsLaunchingMission { get; private set; }

        public SpaceshipTravelService(AppRoot app) => _app = app;

        public async Task EnterAsync(Transform player, Collider2D entrance, string spaceshipScene, string destinationMarker)
        {
            RequireIdle(player);
            if (_app.CurrentMatchId.IsEmpty)
                throw new InvalidOperationException("우주선 임시 방문은 진행 중 Match에서만 가능합니다.");
            var presence = RequirePresence(player);
            if (presence.Location != MatchPlayerLocation.Field || _visitor != null)
                throw new InvalidOperationException("이미 우주선에 방문 중입니다.");
            string key = ResolveScene(spaceshipScene);
            bool ownedScene = !_localScenes.IsLoaded(key);
            Vector3 before = player.position;
            using var input = _app.LocalInput.Acquire("SpaceshipTravel");
            IsTransitioning = true;
            try
            {
                await _app.FadeSpaceshipTravelAsync(async () =>
                {
                    try
                    {
                        await _localScenes.LoadAsync(key, _app.destroyCancellationToken);
                        Transform destination = FindMarker(SceneManager.GetSceneByPath(key), destinationMarker);
                        _returnPosition = SafeReturnPosition(entrance, player, .5f);
                        _fieldScene = player.gameObject.scene.path;
                        _spaceshipScene = key;
                        MoveAndSnap(player, destination.position);
                        Report(presence, MatchPlayerLocation.Spaceship);
                        _visitor = player;
                    }
                    catch
                    {
                        // 실패 복구도 암전 안에서 끝내 카메라가 원위치로 날아가는 장면을 노출하지 않습니다.
                        if (player != null)
                        {
                            MoveAndSnap(player, before);
                            if (presence != null && presence.CanReport) presence.TryReportLocation(MatchPlayerLocation.Field);
                        }
                        _visitor = null;
                        if (ownedScene && _app != null && _localScenes.IsLoaded(key))
                            await _localScenes.UnloadAsync(key, _app.destroyCancellationToken);
                        throw;
                    }
                });
            }
            finally { IsTransitioning = false; }
        }

        public async Task ExitAsync(Transform player, string fieldScene, string returnMarker, float padding)
        {
            RequireIdle(player);
            string key = ResolveScene(fieldScene);
            using var input = _app.LocalInput.Acquire("SpaceshipTravel");
            IsTransitioning = true;
            try
            {
                await _app.FadeSpaceshipTravelAsync(async () =>
                {
                    if (_app.CurrentMatchId.IsEmpty && _app.NetworkMatches?.Descriptor == null)
                    {
                        // 첫 출격은 필드 PlayerStart에서 정식 actor를 만듭니다. 표식은 기존 문 밖 복귀 위치에 둡니다.
                        if (_app.GameFlow == null)
                            throw new InvalidOperationException("Startup에서 구성한 게임 진입 경로가 필요합니다.");
                        IsLaunchingMission = true;
                        try { await _app.GameFlow.LaunchMissionAsync(key, _app.destroyCancellationToken); }
                        finally { IsLaunchingMission = false; }
                    }
                    else
                    {
                        var presence = RequirePresence(player);
                        Scene field = SceneManager.GetSceneByPath(key);
                        if (!field.isLoaded)
                            throw new InvalidOperationException("진행 중 Match의 필드 Scene이 로드되어 있지 않습니다.");
                        Vector3 destination = _visitor == player && _fieldScene == key
                            ? _returnPosition
                            : SafeReturnPosition(FindMarker(field, returnMarker).GetComponent<Collider2D>(), player, padding);
                        MoveAndSnap(player, destination);
                        Report(presence, MatchPlayerLocation.Field);
                        _visitor = null;
                        if (!HasOtherVisitor(player) && !string.IsNullOrEmpty(_spaceshipScene))
                            await _localScenes.UnloadAsync(_spaceshipScene, _app.destroyCancellationToken);
                    }
                });
            }
            finally { IsTransitioning = false; }
        }

        private void RequireIdle(Transform player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (IsTransitioning || _app.GameFlow?.IsTransitioning == true || _app.SceneTransitions.IsTransitioning)
                throw new InvalidOperationException("이미 Scene 이동 중입니다.");
        }

        private static PlayerMatchPresence RequirePresence(Transform player)
        {
            var presence = player.GetComponent<PlayerMatchPresence>();
            if (presence == null || !presence.CanReport)
                throw new InvalidOperationException("우주선 방문에는 Host에서 검증된 Player 위치 보고가 필요합니다. Client 위치 요청은 별도 권위 연결이 필요합니다.");
            return presence;
        }

        private static void Report(PlayerMatchPresence presence, MatchPlayerLocation location)
        {
            MatchPresenceReportStatus status = presence.TryReportLocation(location);
            if (status != MatchPresenceReportStatus.Accepted && status != MatchPresenceReportStatus.Unchanged)
                throw new InvalidOperationException("Player 위치 보고 실패: " + status);
        }

        private static void MoveAndSnap(Transform player, Vector3 destination)
        {
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0;
                body.position = destination;
            }
            player.position = destination;
            Physics2D.SyncTransforms();
            foreach (var follow in UnityEngine.Object.FindObjectsByType<MatchCameraFollow>(FindObjectsSortMode.None))
                if (follow.Target == player) follow.SnapToTarget();
        }

        private static Vector3 SafeReturnPosition(Collider2D entrance, Transform player, float padding)
        {
            if (entrance == null)
                throw new InvalidOperationException("재진입을 피할 필드 출입구 Collider가 필요합니다.");
            float upperOffset = 0;
            foreach (var collider in player.GetComponentsInChildren<Collider2D>())
                if (collider.enabled && !collider.isTrigger)
                    upperOffset = Mathf.Max(upperOffset, collider.bounds.max.y - player.position.y);
            return new Vector3(entrance.bounds.center.x, entrance.bounds.min.y - upperOffset - Mathf.Max(.1f, padding), player.position.z);
        }

        private static Transform FindMarker(Scene scene, string marker)
        {
            Transform found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    if (item.name == marker)
                    {
                        if (found != null) throw new InvalidOperationException("중복 도착 표식: " + marker);
                        found = item;
                    }
            return found != null ? found : throw new InvalidOperationException("도착 표식이 없습니다: " + marker);
        }

        private static string ResolveScene(string name)
        {
            string found = null;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path != name && Path.GetFileNameWithoutExtension(path) != name) continue;
                if (found != null) throw new InvalidOperationException("중복 Scene 이름입니다. 전체 경로를 사용하세요: " + name);
                found = path;
            }
            return found ?? throw new InvalidOperationException("Build Settings에 Scene이 없습니다: " + name);
        }

        private static bool HasOtherVisitor(Transform player)
        {
            foreach (var presence in UnityEngine.Object.FindObjectsByType<PlayerMatchPresence>(FindObjectsSortMode.None))
                if (presence.transform != player && presence.Location == MatchPlayerLocation.Spaceship) return true;
            return false;
        }
    }
}
