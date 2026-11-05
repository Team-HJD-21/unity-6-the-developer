// Feature가 자신의 Match 시간을 읽고 생성한 자식에게 전달하는 공용 함수입니다.
// 전역 Scene/Manager를 검색하지 않고 호출자와 부모의 명시적 바인딩만 읽습니다.
// Player의 필드 쿨다운은 공용 시간을 따르며 허브 이동 허용 여부는 PlayerControlState가 정합니다.
using System;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Presentation.Scene
{
    public static class FieldSimulationTime
    {
        /// <summary>실행 주체가 매 프레임 확인합니다. Match 밖의 독립 Feature 시험은 기존 Unity 시간을 사용합니다.</summary>
        public static bool CanAdvance(Component owner)
        {
            if (owner == null) return false;
            var presence = owner.GetComponentInParent<PlayerMatchPresence>();
            if (presence?.TimeSource != null)
                return !presence.GameplayCancellation.IsCancellationRequested && IsRunning(presence.TimeSource);
            var actor = owner.GetComponentInParent<FieldSimulationActor>();
            return actor == null || actor.CanAdvance;
        }

        /// <summary>쿨다운·수명은 이 절대 게임 시각의 기한으로 계산합니다. 직접 deltaTime을 누적하지 않습니다.</summary>
        public static double Seconds(Component owner)
        {
            var presence = owner.GetComponentInParent<PlayerMatchPresence>();
            if (presence?.TimeSource != null) return presence.FieldSeconds;
            var actor = owner.GetComponentInParent<FieldSimulationActor>();
            return actor != null ? actor.Seconds : Time.timeAsDouble;
        }

        public static float DeltaTime(Component owner) =>
            CanAdvance(owner) ? ScaleDelta(Source(owner), Time.deltaTime) : 0f;

        public static float FixedDeltaTime(Component owner) =>
            CanAdvance(owner) ? ScaleDelta(Source(owner), Time.fixedDeltaTime) : 0f;

        /// <summary>Feature 제어자가 가진 시계로 이동/기존 Tick의 delta를 계산합니다. Unity 배속을 두 번 곱하지 않습니다.</summary>
        public static float ScaleDelta(IMatchTime time, float unityDelta)
        {
            if (time == null) return unityDelta;
            if (!IsRunning(time) || Time.timeScale <= 0) return 0f;
            double delta = unityDelta * time.Snapshot.Rate / Time.timeScale;
            return double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0 || delta > float.MaxValue ? 0f : (float)delta;
        }

        /// <summary>발사/설치/소환 직후 생성자가 호출합니다. 자식은 같은 Scene·시간·Match 종료 수명을 받습니다.</summary>
        public static void BindSpawned(GameObject spawned, Component owner, bool destroyOnCompletion = true)
        {
            if (spawned == null || owner == null)
                throw new ArgumentNullException(spawned == null ? nameof(spawned) : nameof(owner));
            if (spawned.scene != owner.gameObject.scene)
            {
                if (spawned.transform.parent != null)
                    throw new InvalidOperationException("Bind a spawned root in its owner's Scene before parenting it.");
                SceneManager.MoveGameObjectToScene(spawned, owner.gameObject.scene);
            }
            var presence = owner.GetComponentInParent<PlayerMatchPresence>();
            if (presence?.TimeSource != null)
            {
                FieldSimulationActor.Attach(spawned, presence.TimeSource, presence.GameplayCancellation, destroyOnCompletion);
                return;
            }
            var actor = owner.GetComponentInParent<FieldSimulationActor>();
            if (actor?.TimeSource != null)
                FieldSimulationActor.Attach(spawned, actor.TimeSource, actor.GameplayCancellation, destroyOnCompletion);
        }

        private static IMatchTime Source(Component owner)
        {
            var presence = owner.GetComponentInParent<PlayerMatchPresence>();
            return presence?.TimeSource ?? owner.GetComponentInParent<FieldSimulationActor>()?.TimeSource;
        }

        private static bool IsRunning(IMatchTime time) =>
            time.IsAvailable && !time.Snapshot.IsPaused && time.Snapshot.Rate > 0;
    }
}
