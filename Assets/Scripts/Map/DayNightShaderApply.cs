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
        
        [SerializeField] private Material _nightMaterial;
        private Material _runtimeNightMaterial;

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
            if (_runtimeNightMaterial != null) Destroy(_runtimeNightMaterial);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_dayNightCycle != null && _nightMaterial != null)
            {
                if (_runtimeNightMaterial == null)
                    _runtimeNightMaterial = new Material(_nightMaterial);
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
                _runtimeNightMaterial.SetFloat(EffectBlendId, blend);
                Graphics.Blit(source, destination, _runtimeNightMaterial);
            }
            else
                Graphics.Blit(source, destination);
        }
    }
}
