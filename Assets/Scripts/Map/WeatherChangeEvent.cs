using System;
using UnityEngine;

namespace Map
{
    public class WeatherChangeEvent : MonoBehaviour
    {
        public Action<WeatherManager.Weather> ChangeWeatherRain;
        public Action<WeatherManager.Weather> ChangeWeatherThunder;
    }
}