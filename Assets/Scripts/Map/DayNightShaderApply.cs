using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace Map
{
    [RequireComponent(typeof(Camera))]
    public class DayNightShaderApply : MonoBehaviour
    {
        private static readonly int EffectBlendId = Shader.PropertyToID("_EffectBlend");
        [SerializeField] private WeatherManager _weatherManager;
        private IMatchDayNightCycle _dayNightCycle;
        
        [SerializeField] private Material _dayNightMaterial;
        private Material _runtimeDayNightMaterial;

        public bool IsNight { get; private set; }

        private void OnEnable()
        {
            BindCycle();
        }

        private void Update()
        {
            if (_dayNightCycle == null) BindCycle();
        }

        private void BindCycle()
        {
            if (_weatherManager == null || _dayNightCycle != null) return;
            _dayNightCycle = _weatherManager.GetComponent<IMatchDayNightCycle>();
            if (_dayNightCycle == null) return;
            _dayNightCycle.Changed += RefreshPhase;
            RefreshPhase();
        }

        private void RefreshPhase()
        {
            IsNight = _dayNightCycle.Phase == DayNightPhase.Night;
        }

        private void OnDisable()
        {
            if (_dayNightCycle != null) _dayNightCycle.Changed -= RefreshPhase;
            _dayNightCycle = null;
            IsNight = false;
        }

        private void OnDestroy()
        {
            if (_runtimeDayNightMaterial != null) Destroy(_runtimeDayNightMaterial);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_dayNightCycle != null && _dayNightMaterial != null)
            {
                if (_runtimeDayNightMaterial == null)
                    _runtimeDayNightMaterial = new Material(_dayNightMaterial);
                double remaining = _dayNightCycle.RemainingSeconds;
                double dayDuration = _dayNightCycle.DaySeconds;
                double nightDuration = _dayNightCycle.NightSeconds;
                float blend = IsNight ? 1f : 0f;
                if (double.IsFinite(remaining) && dayDuration > 0 && nightDuration > 0)
                {
                    double nightMidpoint = nightDuration * 0.5;
                    if (IsNight)
                    {
                        double elapsed = nightDuration - remaining;
                        blend = elapsed <= nightMidpoint
                            ? (float)((dayDuration / 3d + elapsed) / (dayDuration / 3d + nightMidpoint))
                            : (float)(remaining / nightMidpoint);
                    }
                    else
                    {
                        double elapsed = dayDuration - remaining;
                        blend = (float)((elapsed - dayDuration * 2d / 3d)
                            / (dayDuration / 3d + nightMidpoint));
                    }
                    blend = Mathf.Clamp01(blend);
                }
                _runtimeDayNightMaterial.SetFloat(EffectBlendId, blend);
                Graphics.Blit(source, destination, _runtimeDayNightMaterial);
            }
            else
                Graphics.Blit(source, destination);
        }
    }
}
