using System;
using System.Collections.Generic;
using Map;
using UnityEngine;
using UnityEngine.UI;

/*
 * WeatherManager는 날씨를 정의하고 변경하는 역할만 가지는게 옳다고 생각합니다.
 * 그래서 Tick 세는 걸 분리했습니다.
 */
namespace Map
{
    [RequireComponent(typeof(WeatherChangeEvent))]
    public class WeatherManager : MonoBehaviour
    {
        public enum Weather
        {
            SUNNY,
            RAIN,
            THUNDERSTORM,
            WEATHER_MAX = THUNDERSTORM // Enum의 맨 마지막을 나타내는 열거형 변수
            // WEATHER_MAX - SUNNY 로 날씨가 더 늘어나도 개수를 알 수 있음
        }
        
        [Header("===WeatherManagement===")]
        // 날씨 정보
        [SerializeField] private Weather currentWeather = Weather.SUNNY;
        public Weather CurrentWeather =>  currentWeather;
        [SerializeField, Min(1)] private int weatherQueueSize = 5;
        [SerializeField, Min(1)] private int sunnyWeight = 3;
        private Queue<Weather> weatherQueue;
    
        [Header("===Weather VFX===")]
        // 각 날씨에서 사용할 Particle System => 미정
        [SerializeField] private ParticleSystem rainVFX;
        [SerializeField] private ParticleSystem sunVFX;
        [SerializeField] private ParticleSystem thunderVFX;
        
        [Header("===Weather temporary Canvas ===")]
        [SerializeField] private Image tmpweatherImage;
        [SerializeField] private Color _sunnyColor;
        [SerializeField] private Color _rainColor;
        [SerializeField] private Color _thunderColor;
    
        // 날씨 전환 시 일어날 이벤트 
        public static Action<Weather,Queue<Weather>> OnWeatherChange;
        
        private WeatherChangeEvent _weatherChangeEvent;
        
        void OnEnable()
        {
            OnWeatherChange += WeatherCanvasColorChange;
        }

        void OnDisable()
        {
            OnWeatherChange -= WeatherCanvasColorChange;
        }

        void Awake()
        {
            _weatherChangeEvent = GetComponent<WeatherChangeEvent>();
        }
        void Start()
        {
            FillWeather();
            WeatherCanvasColorChange(currentWeather, weatherQueue);

            if (rainVFX != null && sunVFX!=null && thunderVFX != null)
            {
                rainVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                thunderVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                sunVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void FillWeather()
        {
            if (weatherQueue == null)
            {
                weatherQueue = new Queue<Weather>();
            }

            while (weatherQueue.Count < Mathf.Max(1, weatherQueueSize))
            {
                weatherQueue.Enqueue(GetWeather());
            }
        }

        public void ChangeWeather()
        {
            if (weatherQueue == null || weatherQueue.Count == 0)
            {
                FillWeather();
            }

            currentWeather = weatherQueue.Dequeue();
            Debug.Log($"현재 날씨: {currentWeather}, 뽑힌 후 큐: [{string.Join(", ", weatherQueue)}]");
            FillWeather();
            OnWeatherChange?.Invoke(currentWeather, weatherQueue);
        }

        private Weather GetWeather()
        {
            // SUNNY는 sunnyWeight칸, 다른 날씨는 각각 한 칸을 차지한다.
            int roll = UnityEngine.Random.Range(0, sunnyWeight + (int)Weather.WEATHER_MAX);
            Weather result = roll < sunnyWeight
                ? Weather.SUNNY
                : (Weather)(roll - sunnyWeight + 1);

            return result;
        }
        
        // 날씨 변경 시 호출될 메소드 
        void WeatherCanvasColorChange(Weather curWeather, Queue<Weather> q)
        {
            /*
             * 날씨마다 로직을 여기서 변경 실행이 낫지 않을까
             */
            if (tmpweatherImage == null)
            {
                return;
            }
            switch (curWeather)
            {
                case Weather.SUNNY:
                    tmpweatherImage.color = _sunnyColor;
                    break;
                case Weather.RAIN:
                    tmpweatherImage.color = _rainColor;
                    break;
                case Weather.THUNDERSTORM:
                    tmpweatherImage.color = _thunderColor;
                    break;
                default:
                    tmpweatherImage.color = _sunnyColor;
                    break;
            }
        }
    }
}
