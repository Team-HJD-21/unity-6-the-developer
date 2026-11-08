// Player 장치가 영향 줄 적을 찾습니다. 실행 여부는 같은 필드 시간으로 판단합니다.
using System.Collections.Generic;
using UnityEngine;

/// <summary>장비 주변의 스폰된 서버 네트워크 적에 중재 컴포넌트를 연결합니다.</summary>
public sealed class PlayerFieldEnemyScanner
{
    private readonly HashSet<EnemyNetworkController> _seen = new();
    private float _remaining;
    internal float CaptureCheckpoint() => Mathf.Max(0, _remaining);
    internal void RestoreCheckpoint(float remaining)
    {
        if (!PlayerSkillCheckpointIO.Finite(remaining) || remaining < 0 || remaining > .2f)
            throw new System.IO.InvalidDataException("Invalid saved field target scan delay.");
        _remaining = remaining; _seen.Clear();
    }
    public void Tick(Vector2 position, float radius, float elapsed)
    {
        _remaining -= Mathf.Max(0, elapsed);
        if (_remaining > 0) return;
        _remaining = .2f;
        _seen.Clear();
        foreach (var hit in Physics2D.OverlapCircleAll(position, radius))
        {
            var driver = hit.GetComponentInParent<EnemyNetworkController>();
            if (driver == null || !driver.IsSpawned || !driver.IsServer || !driver.enabled || !_seen.Add(driver)) continue;
            if (driver.GetComponent<PlayerFieldEnemyAdapter>() == null) driver.gameObject.AddComponent<PlayerFieldEnemyAdapter>();
        }
    }
}
