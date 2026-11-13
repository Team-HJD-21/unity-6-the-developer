using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>표시 전용 경계입니다. 위치의 원본을 읽으며 actor 연결이나 전투 권한은 바꾸지 않습니다.</summary>
public static class CombatHudContext
{
    public static bool IsFieldActor(Component actor)
    {
        if (actor == null || !actor.gameObject.activeInHierarchy) return false;
        var presence = actor.GetComponent<PlayerMatchPresence>();
        // PlayerTest 등 위치 서비스가 없는 Feature 씬은 기존 단독 HUD를 유지합니다.
        return presence == null || presence.Location == MatchPlayerLocation.Field;
    }
}
