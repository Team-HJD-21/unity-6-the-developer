using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TeamHJD.Game.FieldTurrets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FieldTurretLedIndicator : MonoBehaviour
    {
        private static readonly int LedTextureId = Shader.PropertyToID("_LedTexture");
        private static readonly Color LockedLightColor = new Color32(199, 33, 33, 255);
        private static readonly Color ActiveLightColor = new Color32(59, 131, 10, 255);

        [SerializeField] private Sprite _lockedSprite;
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;
        [SerializeField] private Light2D _light;

        private FieldTurretBase _turret;
        private SpriteRenderer _renderer;
        private Material _ledMaterial;
        private Sprite _boundSprite;

        internal Light2D Light => _light;

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

        private void OnDisable()
        {
            if (_light != null)
                _light.enabled = false;
        }

        private void OnDestroy()
        {
            if (_ledMaterial != null)
                Destroy(_ledMaterial);
        }

        private void RefreshSprite()
        {
            if (_turret == null) return;
            bool locked = _turret.IsLocked;
            bool activated = _turret.IsActivated && !_turret.IsDestroyed;
            Sprite sprite = locked ? _lockedSprite : activated ? _onSprite : _offSprite;

            if (_light != null)
            {
                bool emit = !_turret.IsDestroyed && (locked || activated) && sprite != null;
                if (emit)
                {
                    Color color = locked ? LockedLightColor : ActiveLightColor;
                    if (_light.color != color)
                        _light.color = color;
                }
                if (_light.enabled != emit)
                    _light.enabled = emit;
            }

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
