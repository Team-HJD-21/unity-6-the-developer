using UnityEngine;

/// <summary>신규 서버 적이 접근하고 공격할 수 있는 플레이어 설치물의 계약입니다.</summary>
public interface IPlayerFieldTarget
{
    PlayerFieldDeviceKind Kind { get; }
    bool IsAlive { get; }
    Vector3 Position { get; }
    float Radius { get; }
    Vector2 ApproachPoint(Vector2 origin);
    bool TryGetContact(Collider2D body, out ColliderDistance2D contact);
    bool Intersects(Vector2 origin, Vector2 goal, out float fraction, Collider2D body = null);
    void TakeDamage(float damage);
}
