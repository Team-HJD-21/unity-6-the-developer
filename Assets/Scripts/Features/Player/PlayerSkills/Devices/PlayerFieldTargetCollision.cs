using System.Collections.Generic;
using UnityEngine;

/// <summary>프리팹에 배치된 장비 콜라이더로 경로와 몸통 사이 거리를 조회합니다.</summary>
public sealed class PlayerFieldTargetCollision
{
    private readonly List<Collider2D> _colliders = new(3);
    private readonly List<RaycastHit2D> _hits = new(4);

    public bool HasColliders => _colliders.Count > 0;

    public PlayerFieldTargetCollision(Collider2D blocker, IReadOnlyList<Collider2D> bodies = null)
    {
        if (blocker != null) _colliders.Add(blocker);
        if (bodies == null) return;
        foreach (var body in bodies)
            if (body != null && !_colliders.Contains(body)) _colliders.Add(body);
    }

    public static bool IsBody(Collider2D collider) =>
        collider != null && collider.isActiveAndEnabled && !collider.isTrigger;

    public bool TryGetContact(Collider2D body, out ColliderDistance2D contact)
    {
        contact = default;
        if (!IsBody(body)) return false;
        // 장비 AI는 Update에서 Transform을 이동하므로 현재 자세를 물리 조회에 반영합니다.
        Physics2D.SyncTransforms();
        foreach (var target in _colliders)
        {
            if (!IsBody(target)) continue;
            var distance = body.Distance(target);
            if (distance.isValid && (!contact.isValid || distance.distance < contact.distance)) contact = distance;
        }
        return contact.isValid;
    }

    public bool Intersects(Vector2 origin, Vector2 goal, out float fraction, Collider2D body = null)
    {
        fraction = float.PositiveInfinity;
        Rigidbody2D actorBody = IsBody(body) ? body.attachedRigidbody : null;
        if (actorBody != null && (goal - origin).sqrMagnitude < .000001f)
        {
            // 이미 본체에 막힌 뒤 연결된 적도 물리 접촉 오차 안에서는 공격을 시작할 수 있습니다.
            if (TryGetContact(body, out var contact) && contact.distance <= Physics2D.defaultContactOffset * 2)
            { fraction = 0; return true; }
            return false;
        }
        int layers = 0;
        foreach (var target in _colliders)
        {
            if (!IsBody(target)) continue;
            if (actorBody == null && target.OverlapPoint(origin)) { fraction = 0; return true; }
            layers |= 1 << target.gameObject.layer;
        }
        if (layers == 0) return false;

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(layers);
        Vector2 delta = goal - origin;
        if (actorBody != null)
        {
            // 중심 선분이 본체 옆을 지나도 적 몸통이 걸리는 경로는 공격 대상으로 선택합니다.
            // 위치와 각도는 자식 Collider가 아닌 Rigidbody 루트를 기준으로 전달합니다.
            body.Cast(origin, actorBody.transform.eulerAngles.z, delta.normalized, filter, _hits, delta.magnitude);
        }
        else Physics2D.Linecast(origin, goal, filter, _hits);

        foreach (var hit in _hits)
        {
            if (IsBody(hit.collider) && _colliders.Contains(hit.collider)) fraction = Mathf.Min(fraction, hit.fraction);
        }
        return !float.IsPositiveInfinity(fraction);
    }
}
