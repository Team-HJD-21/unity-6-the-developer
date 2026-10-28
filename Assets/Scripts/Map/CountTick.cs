// Weather Feature의 현재 Match clock/권한에 따라 간격 진행을 요청합니다.
// Host/Client·완료 판정과 Random은 WeatherManager의 lease가 소유합니다.
using UnityEngine;

namespace Map
{
    public class CountTick : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float weatherChangeTime = 5f;
        
        private WeatherManager _weatherManager;
        void Awake()
        {
            _weatherManager = GetComponent<WeatherManager>();
        }
        void Update()
        {
            if (_weatherManager != null) _weatherManager.TickWeather(weatherChangeTime, Time.deltaTime);
        }
    }
}
