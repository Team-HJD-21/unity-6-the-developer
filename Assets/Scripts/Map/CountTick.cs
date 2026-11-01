using System.Collections;
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
        void Start()
        {
            StartCoroutine(StartWeather());
        }

        IEnumerator StartWeather()
        {
            while (true)
            {
                yield return new WaitForSeconds(weatherChangeTime);
                _weatherManager.ChangeWeather();
            }
        }
    }
}
