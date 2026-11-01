// Player의 필드 효과·설치물에 Match 시계와 종료 수명을 전달합니다.
// 플레이어 이동/UI와 분리하여 쿨다운·기존 필드 효과는 공유 필드 시간만 따릅니다.
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public static class PlayerFieldSimulationUtility
{
    public static bool CanAdvance(Component component)
    {
        var presence = component.GetComponentInParent<PlayerMatchPresence>();
        var time = presence?.TimeSource;
        if (time != null) return !presence.GameplayCancellation.IsCancellationRequested && time.IsAvailable && !time.Snapshot.IsPaused && time.Snapshot.Rate > 0;
        var actor = component.GetComponentInParent<FieldSimulationActor>();
        return actor == null || actor.CanAdvance;
    }
    public static double Seconds(Component component)
    {
        var presence = component.GetComponentInParent<PlayerMatchPresence>();
        if (presence?.TimeSource != null) return presence.FieldSeconds;
        return component.GetComponentInParent<FieldSimulationActor>()?.Seconds ?? Time.timeAsDouble;
    }
    /// <summary>발사·설치 직후 생성자가 호출합니다. 원본 Scene을 검색하지 않고 전달받은 소유자에서만 시계를 읽습니다.</summary>
    public static void BindSpawned(GameObject spawned, Component owner)
    {
        var presence = owner.GetComponentInParent<PlayerMatchPresence>();
        if (presence?.TimeSource != null)
            FieldSimulationActor.Attach(spawned, presence.TimeSource, presence.GameplayCancellation, destroyOnCompletion: true);
        else
        {
            var actor = owner.GetComponentInParent<FieldSimulationActor>();
            if (actor?.TimeSource != null)
                FieldSimulationActor.Attach(spawned, actor.TimeSource, actor.GameplayCancellation, destroyOnCompletion: true);
        }
    }
}
