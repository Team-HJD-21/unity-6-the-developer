using System.Collections.Generic;
using UnityEngine;

/// <summary>피해 판정을 바꾸지 않고 설치물 본체의 커서 체력 표시 영역을 제공합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerInstallationHealthTarget : MonoBehaviour
{
    private static readonly HashSet<PlayerInstallationHealthTarget> Active = new();
    internal static IEnumerable<PlayerInstallationHealthTarget> ActiveInstances => Active;
    private PlayerFieldDevice _device;
    private PlayerTurretHealth _turret;
    private SpriteRenderer[] _bodies;
    private Collider2D _membrane;
    public bool IsAlive => _device != null ? _device.IsAlive : _turret != null && _turret.IsAlive;
    public float Health => _device != null ? _device.Health : _turret != null ? _turret.Health : 0;
    public float MaxHealth => _device != null ? _device.MaxHealth : _turret != null ? _turret.MaxHealth : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();
    private void Awake()
    {
        _device = GetComponent<PlayerFieldDevice>(); _turret = GetComponent<PlayerTurretHealth>();
        _bodies = GetComponentsInChildren<SpriteRenderer>(true);
        _membrane = GetComponent<Collider2D>();
    }
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
    private void OnDestroy() => Active.Remove(this);

    public bool TryHover(Vector2 pointer, out Vector3 anchor)
    {
        anchor = default;
        if (!IsAlive) return false;
        foreach (var body in _bodies)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy || body.sprite == null) continue;
            var bounds = body.bounds;
            if (pointer.x < bounds.min.x || pointer.x > bounds.max.x || pointer.y < bounds.min.y || pointer.y > bounds.max.y) continue;
            anchor = new Vector3(bounds.center.x, bounds.max.y + .2f, transform.position.z);
            return true;
        }
        // 방벽은 연결막 위에서도 같은 공유 체력을 보여줍니다.
        if (_device != null && _device.Kind == PlayerFieldDeviceKind.Barrier &&
            _membrane != null && _membrane.enabled && _membrane.OverlapPoint(pointer))
        { anchor = new Vector3(pointer.x, pointer.y + .65f, transform.position.z); return true; }
        return false;
    }
}
