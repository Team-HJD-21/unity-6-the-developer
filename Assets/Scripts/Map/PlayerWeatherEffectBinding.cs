using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

// 씬이 지정한 로컬 플레이어의 위치 상태를 카메라 날씨 효과에 전달한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(WeatherShaderApply))]
public sealed class PlayerWeatherEffectBinding : MonoBehaviour, ILocalPlayerActorConsumer
{
    private WeatherShaderApply _weatherEffects;
    private PlayerLocationState _location;
    private MatchId _matchId;
    private bool _bound;

    private void Awake()
    {
        _weatherEffects = GetComponent<WeatherShaderApply>();
    }

    public void PrepareLocalActor(MatchId matchId, PlayerId playerId)
    {
        // 새 로컬 플레이어를 연결하기 전에 이전 연결을 정리한다.
        ClearBinding();
    }

    public void BindLocalActor(MatchId matchId, PlayerId playerId, GameObject actor)
    {
        ClearBinding();
        if (actor == null)
            return;

        _location = actor.GetComponent<PlayerLocationState>();
        if (_location == null)
        {
            Debug.LogWarning("로컬 플레이어에 PlayerLocationState가 없습니다.", actor);
            return;
        }

        _matchId = matchId;
        _bound = true;
        // 위치가 바뀔 때 알림을 받고, 현재 위치도 즉시 반영한다.
        _location.LocationChanged += OnLocationChanged;
        OnLocationChanged(_location.IsInSpaceship);
    }

    public void UnbindLocalActor(MatchId matchId)
    {
        if (_bound && _matchId == matchId)
            ClearBinding();
    }

    private void OnLocationChanged(bool isInSpaceship)
    {
        // 우주선 내부에서는 카메라의 날씨 효과만 숨긴다.
        _weatherEffects.SetEffectsEnabled(!isInSpaceship);
    }

    private void ClearBinding()
    {
        // 이전 플레이어의 알림을 해제해 다른 플레이어가 화면을 바꾸지 않게 한다.
        if (_location != null)
            _location.LocationChanged -= OnLocationChanged;
        _location = null;
        _bound = false;
        if (_weatherEffects != null)
            _weatherEffects.SetEffectsEnabled(true);
    }

    private void OnDestroy()
    {
        ClearBinding();
    }
}
