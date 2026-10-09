using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥 타일이 있는 셀에 고정 장애물 콜라이더가 닿는지 검사한다.
/// A*은 CanOccupy가 false인 셀을 이웃 후보에서 제외한다.
/// </summary>
public static class EnemyPositionChecker
{
    // 여러 셀을 검사할 때 결과 목록을 재사용한다.
    private static readonly List<Collider2D> Hits = new(32);

    public static bool CanOccupy(EnemyNavigationMap map, Vector3Int cell)
    {
        if (map == null || !map.IsWalkable(cell))
            return false;

        // 셀 전체를 검사하므로 얇은 Outline 경계가 셀을 지나가도 막힌 셀로 표시된다.
        // 몬스터 몸체 크기로 검사하면 경계가 지나가는 셀이 초록색으로 남을 수 있다.
        Hits.Clear();
        Physics2D.OverlapBox(
            (Vector2)map.CellToWorld(cell),
            map.CellSizeWorld,
            0f,
            new ContactFilter2D
            {
                useLayerMask = false,
                useDepth = false,
                useNormalAngle = false,
                useTriggers = true
            },
            Hits
        );

        foreach (Collider2D hit in Hits)
        {
            if (hit == null || hit.isTrigger)
                continue;

            // 몬스터/플레이어 같은 Dynamic 몸체는 고정 장애물로 보지 않는다.
            // Rigidbody가 없는 콜라이더와 Static/Kinematic(터렛 포함)은 막는다.
            Rigidbody2D rigidbody = hit.attachedRigidbody;
            if (rigidbody == null ||
                rigidbody.bodyType != RigidbodyType2D.Dynamic)
                return false;
        }

        return true;
    }
}