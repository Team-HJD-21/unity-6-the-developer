using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FieldTurretLedIndicator : MonoBehaviour
    {
        [SerializeField] private Sprite _lockedSprite;
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        private FieldTurretBase _turret;
        private SpriteRenderer _renderer;

        private void Awake()
        {
            _turret = GetComponentInParent<FieldTurretBase>();
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable() => RefreshSprite();

        private void LateUpdate() => RefreshSprite();

        private void RefreshSprite()
        {
            if (_turret == null) return;

            Sprite sprite = _offSprite;
            if (_turret.IsLocked)
                sprite = _lockedSprite;
            else if (_turret.IsActivated)
                sprite = _onSprite;

            if (_renderer.sprite != sprite)
                _renderer.sprite = sprite;
        }
    }
}
