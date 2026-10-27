using System.Collections.Generic;
using UnityEngine;

/// <summary>프리팹의 본체 충돌과 설치 순간 겹친 플레이어의 일회성 탈출을 관리합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerFieldBodyCollision : MonoBehaviour
{
    [SerializeField] private Collider2D[] _bodies;
    private readonly List<Collider2D[]> _exitingPlayers = new();
    private const float ExitClearance = .02f;

    /// <summary>본체 위치 확정 후 한 번 호출합니다. 새로 접근하는 플레이어는 통과시키지 않습니다.</summary>
    public void AllowInitialExit()
    {
        RestoreCollisions();
        if (_bodies == null) return;
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0) return;
        Physics2D.SyncTransforms();
        var players = new HashSet<PlayerInputHandler>();
        foreach (var body in _bodies)
        {
            if (body == null || !body.isActiveAndEnabled) continue;
            foreach (var hit in Physics2D.OverlapBoxAll(body.bounds.center, body.bounds.size, 0f, 1 << playerLayer))
            {
                if (hit.isTrigger || !body.Distance(hit).isOverlapped) continue;
                var player = hit.GetComponentInParent<PlayerInputHandler>();
                if (player != null) players.Add(player);
            }
        }
        foreach (var player in players)
        {
            // 비활성 방향도 보관해야 본체 안에서 방향을 바꿀 때 탈출 처리가 유지됩니다.
            var colliders = player.GetComponentsInChildren<Collider2D>(true);
            _exitingPlayers.Add(colliders);
            SetIgnored(colliders, true);
        }
    }

    private void FixedUpdate()
    {
        for (int i = _exitingPlayers.Count - 1; i >= 0; i--)
        {
            var player = _exitingPlayers[i];
            bool overlapping = IsInside(player);
            // Collider가 꺼지면 무시 설정도 해제되므로 방향 전환 후 다시 적용합니다.
            SetIgnored(player, overlapping);
            if (!overlapping) _exitingPlayers.RemoveAt(i);
        }
    }

    private bool IsInside(Collider2D[] player)
    {
        foreach (var body in _bodies)
        {
            if (body == null || !body.isActiveAndEnabled) continue;
            foreach (var collider in player)
            {
                if (!IsPlayerBody(collider) || !collider.isActiveAndEnabled) continue;
                var distance = body.Distance(collider);
                if (distance.isValid && distance.distance <= ExitClearance) return true;
            }
        }
        return false;
    }

    private void SetIgnored(Collider2D[] player, bool ignored)
    {
        foreach (var body in _bodies)
        {
            if (body == null || !body.isActiveAndEnabled) continue;
            foreach (var collider in player)
                if (IsPlayerBody(collider) && collider.isActiveAndEnabled)
                    Physics2D.IgnoreCollision(body, collider, ignored);
        }
    }

    private static bool IsPlayerBody(Collider2D collider) => collider != null && !collider.isTrigger;

    public Vector2 ClosestPoint(Vector2 origin)
    {
        Vector2 closest = transform.position;
        float nearest = float.PositiveInfinity;
        if (_bodies == null) return closest;
        foreach (var body in _bodies)
        {
            if (body == null || !body.isActiveAndEnabled) continue;
            Vector2 point = body.ClosestPoint(origin);
            float distance = (point - origin).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            closest = point;
        }
        return closest;
    }

    private void RestoreCollisions()
    {
        foreach (var player in _exitingPlayers) SetIgnored(player, false);
        _exitingPlayers.Clear();
    }

    private void OnDisable() => RestoreCollisions();
}
