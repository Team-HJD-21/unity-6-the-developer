// 실제 날씨 선택·예고·표현을 소유합니다. Core 부분 파일은 예약과 환경 전달만 연결합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

/*
 * WeatherManager는 날씨를 정의하고 변경하는 역할만 가지는게 옳다고 생각합니다.
 * 그래서 Tick 세는 걸 분리했습니다.
 */
namespace Map
{
    public partial class WeatherManager : MonoBehaviour, IRandomConsumer
    {
        [Serializable]
        private struct WeatherWeight
        {
            public Weather weather;
            [Min(0)] public int weight;
        }

        private DayNightPhase? _queuedPhase;
        private IMatchDayNightCycle _dayNight;

        public enum Weather
        {
            SUNNY,
            RAIN,
            THUNDERSTORM,
            HEATWAVE,
            WEATHER_COUNT = HEATWAVE + 1 // 날씨 종류의 전체 개수
        }
        
        [Header("===WeatherManagement===")]
        // 날씨 정보
        [SerializeField] private Weather currentWeather = Weather.SUNNY;
        public Weather CurrentWeather =>  currentWeather;
        [SerializeField, Min(1)] private int weatherQueueSize = 5;
        private Queue<Weather> weatherQueue;

        [Header("Day Weather Weights")]
        [SerializeField] private WeatherWeight[] _dayWeatherWeights =
        {
            new WeatherWeight { weather = Weather.SUNNY, weight = 5 },
            new WeatherWeight { weather = Weather.RAIN, weight = 2 },
            new WeatherWeight { weather = Weather.THUNDERSTORM, weight = 1 },
            new WeatherWeight { weather = Weather.HEATWAVE, weight = 2 }
        };

        [Header("Night Weather Weights")]
        [SerializeField] private WeatherWeight[] _nightWeatherWeights =
        {
            new WeatherWeight { weather = Weather.SUNNY, weight = 6 },
            new WeatherWeight { weather = Weather.RAIN, weight = 2 },
            new WeatherWeight { weather = Weather.THUNDERSTORM, weight = 2 },
            new WeatherWeight { weather = Weather.HEATWAVE, weight = 0 }
        };
        
        [SerializeField] private WeatherShaderApply _weatherShaderApply;
        
        // Core가 주입하는 Match 전용 날씨 난수.
        private IRandomSource _random;
        private bool CanDrawWeather => _random != null ? _random.IsAvailable : !_coreRequiresMatch;

        void Awake()
        {
            if (_weatherShaderApply == null || !_weatherShaderApply.CompareTag("MainCamera"))
            {
                Camera mainCamera = Camera.main;
                _weatherShaderApply = mainCamera != null
                    ? mainCamera.GetComponent<WeatherShaderApply>()
                    : null;
            }
        }
        void Start()
        {
            if (!MayChangeCoreWeather) return;
            FillWeather();
            ApplyWeatherShader();
        }

        private void FillWeather()
        {
            if (!CanDrawWeather)
                return;

            if (weatherQueue == null)
            {
                weatherQueue = new Queue<Weather>();
            }
            // 낮밤 별도 날씨
            DayNightPhase phase = GetDayNightPhase();
            if (_queuedPhase != phase)
            {
                weatherQueue.Clear();
                _queuedPhase = phase;
            }

            while (weatherQueue.Count < Mathf.Max(1, weatherQueueSize))
            {
                weatherQueue.Enqueue(GetWeather(phase));
            }
        }

        public void ChangeWeather()
        {
            if (!MayChangeCoreWeather || !CanDrawWeather)
                return;

            // 낮밤이 바뀌었다면 이전 시간대의 예고를 버리고 다시 추첨한다.
            FillWeather();

            currentWeather = weatherQueue.Dequeue();
            Debug.Log($"현재 날씨: {currentWeather}, 뽑힌 후 큐: [{string.Join(", ", weatherQueue)}]");
            FillWeather();
            ApplyWeatherShader();
            PublishCoreWeather();
        }
        
        private void ApplyWeatherShader()
        {
            if (_weatherShaderApply != null)
                _weatherShaderApply.ApplyWeather(currentWeather);
        }

        private DayNightPhase GetDayNightPhase()
        {
            _dayNight ??= GetComponent<IMatchDayNightCycle>();
            return _dayNight?.Phase ?? DayNightPhase.Day;
        }

        private Weather GetWeather(DayNightPhase phase)
        {
            WeatherWeight[] weatherWeights = phase == DayNightPhase.Night
                ? _nightWeatherWeights
                : _dayWeatherWeights;
            int totalWeight = 0;
            for (int i = 0; i < weatherWeights.Length; i++)
            {
                totalWeight += Mathf.Max(0, weatherWeights[i].weight);
            }

            if (totalWeight <= 0)
            {
                Debug.LogError($"{phase} 날씨 가중치 합은 1 이상이어야 합니다.", this);
                return Weather.SUNNY;
            }

            // Match 밖의 독립 Scene만 Unity 난수로 추첨한다. Match에서는 주입된 스트림을 사용한다.
            int selectedWeight = _random != null
                ? _random.NextInt(0, totalWeight)
                : UnityEngine.Random.Range(0, totalWeight);
            for (int i = 0; i < weatherWeights.Length; i++)
            {
                selectedWeight -= Mathf.Max(0, weatherWeights[i].weight);
                if (selectedWeight < 0)
                    return weatherWeights[i].weather;
            }

            return Weather.SUNNY;
        }
        public void CallWeatherChange()
        {
            ChangeWeather();
        }

        public IDisposable BindRandom(IRandomStreams streams)
        {
            IDisposable lease = RandomBinding.Bind(streams, "weather", ref _random);
            try
            {
                weatherQueue?.Clear();
                _queuedPhase = null;
                FillWeather();
                return lease;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
    }
}
