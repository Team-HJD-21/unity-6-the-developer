// 실제 날씨 선택·예고·표현을 소유합니다. Core 부분 파일은 예약과 환경 전달만 연결합니다.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * WeatherManager는 날씨를 정의하고 변경하는 역할만 가지는게 옳다고 생각합니다.
 * 그래서 Tick 세는 걸 분리했습니다.
 */
namespace Map
{
    [RequireComponent(typeof(WeatherChangeEvent))]
    public partial class WeatherManager : MonoBehaviour
    {
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
        [SerializeField, Min(1)] private int sunnyWeight = 3;
        private Queue<Weather> weatherQueue;
        
        [Header("===Weather temporary Canvas ===")]
        [SerializeField] private Image tmpweatherImage;
        [SerializeField] private Color _sunnyColor;
        [SerializeField] private Color _rainColor;
        [SerializeField] private Color _thunderColor;
        [SerializeField] private Color _heatWaveColor = new Color(1f, 0.28f, 0.38f, 0.75f);
    
        // 날씨 전환 시 일어날 이벤트 
        public static Action<Weather,Queue<Weather>> OnWeatherChange;
        
        private WeatherChangeEvent _weatherChangeEvent;

        [SerializeField] private WeatherShaderApply _weatherShaderApply;
        
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
            WeatherCanvasColorChange(currentWeather, weatherQueue);
            ApplyWeatherShader();
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
            if (!MayChangeCoreWeather) return;
            if (weatherQueue == null || weatherQueue.Count == 0)
            {
                FillWeather();
            }

            currentWeather = weatherQueue.Dequeue();
            Debug.Log($"현재 날씨: {currentWeather}, 뽑힌 후 큐: [{string.Join(", ", weatherQueue)}]");
            FillWeather();
            ApplyWeatherShader();
            PublishCoreWeather();
            OnWeatherChange?.Invoke(currentWeather, weatherQueue);
        }
        
        private void ApplyWeatherShader()
        {
            if (_weatherShaderApply != null)
                _weatherShaderApply.ApplyWeather(currentWeather);
        }

        private Weather GetWeather()
        {
            // SUNNY는 sunnyWeight칸, 다른 날씨는 각각 한 칸을 차지한다.
            int roll = UnityEngine.Random.Range(0, sunnyWeight + (int)Weather.WEATHER_COUNT - 1);
            Weather result = roll < sunnyWeight
                ? Weather.SUNNY
                : (Weather)(roll - sunnyWeight + 1);

            return result;
        }
        
        // 날씨 변경 시 호출될 메소드 
        void WeatherCanvasColorChange(Weather curWeather, Queue<Weather> q)
        {
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
                case Weather.HEATWAVE:
                    tmpweatherImage.color = _heatWaveColor;
                    break;
                default:
                    tmpweatherImage.color = _sunnyColor;
                    break;
            }
        }
        public void CallWeatherChange()
        {
            ChangeWeather();
        }
    }
}
