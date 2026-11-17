using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FieldTurretLedIndicator : MonoBehaviour
    {
        private static readonly int LedTextureId = Shader.PropertyToID("_LedTexture");

        [SerializeField] private Sprite _lockedSprite;
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        private FieldTurretBase _turret;
        private SpriteRenderer _renderer;
        private Material _ledMaterial;
        private Sprite _boundSprite;

        private void Awake()
        {
            _turret = GetComponentInParent<FieldTurretBase>();
            _renderer = GetComponent<SpriteRenderer>();

            Shader shader = Resources.Load<Shader>("FieldTurretLed");
            if (shader != null)
                _ledMaterial = new Material(shader);
            else
                Debug.LogError("The FieldTurretLed shader is missing from Resources.", this);
        }

        private void OnEnable() => RefreshSprite();
        private void LateUpdate() => RefreshSprite();

        private void OnDestroy()
        {
            if (_ledMaterial != null)
                Destroy(_ledMaterial);
        }

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

            if (_ledMaterial == null || sprite == null || _boundSprite == sprite) return;
            _boundSprite = sprite;
            _ledMaterial.SetTexture(LedTextureId, sprite.texture);
            if (_renderer.sharedMaterial != _ledMaterial)
                _renderer.sharedMaterial = _ledMaterial;
        }
    }
}
