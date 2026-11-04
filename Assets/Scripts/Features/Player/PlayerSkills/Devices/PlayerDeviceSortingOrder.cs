using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>설치물의 접지점과 설치자의 발밑 Y를 비교해 SortingGroup의 앞뒤 표시 순서를 갱신합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(250)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "PlayerSkillDepth")]
public sealed class PlayerDeviceSortingOrder : MonoBehaviour
{
    [Serializable]
    private sealed class SortingPart
    {
        public SortingGroup sorting = null;
        public Collider2D ground = null;
    }

    [SerializeField] private SortingPart[] _parts;
    private Transform _player;
    private SpriteRenderer _playerBody;
    private PlayerDirectionalColliders _playerColliders;

    public void Bind(Transform player)
    {
        _player = player;
        _playerBody = player != null ? player.GetComponent<SpriteRenderer>() : null;
        _playerColliders = player != null ? player.GetComponent<PlayerDirectionalColliders>() : null;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (_player == null || _playerBody == null || _parts == null) return;
        float playerY = _playerColliders != null ? _playerColliders.GroundPosition.y : _player.position.y;
        foreach (var part in _parts)
        {
            if (part == null || part.sorting == null) continue;
            float groundY = part.ground != null ? part.ground.bounds.center.y : part.sorting.transform.position.y;
            part.sorting.sortingLayerID = _playerBody.sortingLayerID;
            part.sorting.sortingOrder = _playerBody.sortingOrder + (groundY >= playerY ? -1 : 1);
        }
    }
}
