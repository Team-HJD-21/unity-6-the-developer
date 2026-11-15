// 카메라의 낮밤 색상과 로컬 Player 주변의 야간 가시 범위를 표현합니다.
// 조립부가 주입한 Player를 읽으며 HP·점유·시간·날씨 원본은 변경하지 않습니다.
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Serialization;

namespace Map
{
    [RequireComponent(typeof(Camera))]
    public class DayNightShaderApply : MonoBehaviour, ILocalPlayerActorConsumer
    {
        private static readonly int EffectBlendId = Shader.PropertyToID("_EffectBlend");
        [SerializeField] private WeatherManager _weatherManager;
        private IMatchDayNightCycle _dayNightCycle;
        
        [FormerlySerializedAs("_daynightMaterial")]
        [SerializeField] private Material _dayNightMaterial;
        private Material _runtimeDayNightMaterial;

        [Header("야간 Player 조명 · 월드 단위")]
        [Tooltip("이 반경 안에서는 같은 날씨의 낮 밝기를 유지합니다.")]
        [SerializeField, Min(0f)] private float _playerLightRadius = 5f;
        [Tooltip("안쪽 반경에서 기존 밤 밝기까지 부드럽게 줄어드는 추가 거리입니다.")]
        [SerializeField, Min(.01f)] private float _playerLightFalloff = 4f;
        private Camera _camera;
        private GameObject _lightActor;
        private PlayerLocationState _lightLocation;
        private PlayerInfo _lightHealth;
        private MatchId _lightMatch;

        public void PrepareLocalActor(MatchId matchId, PlayerId playerId) => ClearPlayerLight();

        /// <summary>Core의 Scene 조립부가 현재 로컬 Player를 전달합니다. 원격 Player의 조명을 대신 만들지 않습니다.</summary>
        public void BindLocalActor(MatchId matchId, PlayerId playerId, GameObject actor)
        {
            ClearPlayerLight();
            if (actor == null) return;
            _lightMatch = matchId; _lightActor = actor;
            _lightLocation = actor.GetComponent<PlayerLocationState>();
            _lightHealth = actor.GetComponent<PlayerInfo>();
        }

        public void UnbindLocalActor(MatchId matchId)
        {
            if (_lightMatch == matchId) ClearPlayerLight();
        }

        private void ClearPlayerLight()
        { _lightActor = null; _lightLocation = null; _lightHealth = null; _lightMatch = default; }

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
            ClearPlayerLight();
            if (_runtimeDayNightMaterial != null) Destroy(_runtimeDayNightMaterial);
        }

        internal Material GetRenderMaterial()
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
                UpdatePlayerLight(_runtimeDayNightMaterial);
                return _runtimeDayNightMaterial;
            }

            return null;
        }

        private void UpdatePlayerLight(Material material)
        {
            _camera ??= GetComponent<Camera>();
            bool visible = _camera.orthographic && _lightActor != null && _lightActor.activeInHierarchy &&
                (_lightLocation == null || !_lightLocation.IsInSpaceship) && (_lightHealth == null || _lightHealth.curHp > 0);
            Vector3 position = visible ? _lightActor.transform.position : Vector3.zero;
            float radius = float.IsFinite(_playerLightRadius) ? Mathf.Max(0, _playerLightRadius) : 0;
            float falloff = float.IsFinite(_playerLightFalloff) ? Mathf.Max(.01f, _playerLightFalloff) : 4;
            material.SetVector("_PlayerLightPosition", position);
            material.SetVector("_PlayerLightRange", new Vector4(radius, radius + falloff, visible && radius > 0 ? 1 : 0, 0));
        }
    }
}
