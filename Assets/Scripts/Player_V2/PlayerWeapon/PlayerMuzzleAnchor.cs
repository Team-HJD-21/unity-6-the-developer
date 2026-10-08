using UnityEngine;
using UnityEngine.Serialization;

/// <summary>기존 발사점 참조를 실제 활성 총구에 맞춥니다. 총구가 없으면 몸에서 사격하지 않습니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class PlayerMuzzleAnchor : MonoBehaviour
{
    [FormerlySerializedAs("playerpoint")]
    [SerializeField] private Transform _playerRoot;
    [SerializeField] private PlayerWeaponRig _weaponRig;
    public PlayerWeaponRig WeaponRig => _weaponRig;

    private void Awake()
    {
        if (_playerRoot == null) _playerRoot = transform.parent;
        if (_weaponRig == null && _playerRoot != null)
            _weaponRig = _playerRoot.GetComponentInChildren<PlayerWeaponRig>(true);
        Synchronize();
    }

    private void LateUpdate() => Synchronize();

    public bool TryGetMuzzle(out Transform muzzle)
    {
        muzzle = null;
        if (!isActiveAndEnabled || _weaponRig == null || _weaponRig.PlayerRoot != _playerRoot ||
            !_weaponRig.TryGetMuzzle(out muzzle)) return false;
        transform.position = muzzle.position;
        return true;
    }

    private void Synchronize() => TryGetMuzzle(out _);
}
