using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>장비의 접지점을 로컬 플레이어 발밑과 비교해 장비를 앞·뒤로 표시합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(250)]
public sealed class PlayerSkillDepth : MonoBehaviour
{
    [Serializable]
    private sealed class Part
    {
        public SortingGroup sorting = null;
        public Collider2D ground = null;
    }

    [SerializeField] private Part[] _parts;
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
