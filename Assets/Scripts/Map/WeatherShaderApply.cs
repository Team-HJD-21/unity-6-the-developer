using Map;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Camera))]
public class WeatherShaderApply : MonoBehaviour
{
    private static readonly int _currentStrikeRootsId = Shader.PropertyToID("_CurrentStrikeRoots");
    private static readonly int _playerWorldHeightId = Shader.PropertyToID("_PlayerWorldHeight");
    private static readonly int _lightningSizeId = Shader.PropertyToID("_LightningSize");
    private static readonly int _spawnAboveViewportId = Shader.PropertyToID("_SpawnAboveViewport");
    private readonly Vector4[] _currentStrikeRoots = new Vector4[3];
    private Camera _weatherCamera;
    private int _currentStrikeIndex = int.MinValue;
    [FormerlySerializedAs("sunnyMaterial")]
    [SerializeField] private Material _sunnyMaterial;
    [FormerlySerializedAs("rainMaterial")]
    [SerializeField] private Material _rainMaterial;
    [FormerlySerializedAs("thunderMaterial")]
    [SerializeField] private Material _thunderMaterial;
    [FormerlySerializedAs("heatWaveMaterial")]
    [SerializeField] private Material _heatWaveMaterial;
    // 전환 보간용 .mat
    [FormerlySerializedAs("transitionMaterial")]
    [SerializeField] private Material _transitionMaterial;
    // 보간 시간
    [FormerlySerializedAs("transitionDuration")]
    [SerializeField, Min(0f)] private float _transitionDuration = 1.5f;
    
    private WeatherManager.Weather _currentWeather = WeatherManager.Weather.SUNNY;
    private WeatherManager.Weather _previousWeather = WeatherManager.Weather.SUNNY;
    private float _transitionStartTime;
    private bool _hasInitialWeather;
    private bool _effectsEnabled = true;

    private void Awake()
    {
        // 잘못된 오브젝트에 붙은 WeatherShaderApply가 추가한 카메라는 화면을 덮지 않게 한다.
        if (CompareTag("MainCamera"))
        {
            _weatherCamera = GetComponent<Camera>();
            return;
        }

        Camera attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null)
            attachedCamera.enabled = false;
        enabled = false;
    }

    public void ApplyWeather(WeatherManager.Weather weather)
    {
        if (_hasInitialWeather && _currentWeather == weather)
            return;

        // 이전 날씨를 기억하고 전환 시작 시각을 기록한다.
        _previousWeather = _currentWeather;
        _currentWeather = weather;
        _transitionStartTime = Time.time;
        // 처음 설정하는 날씨는 이전 화면이 없으므로 즉시 적용한다.
        if (!_hasInitialWeather)
        {
            _previousWeather = weather;
            _hasInitialWeather = true;
        }
        _currentStrikeIndex = int.MinValue;
    }

    public void SetEffectsEnabled(bool enabled)
    {
        if (_effectsEnabled == enabled)
            return;

        _effectsEnabled = enabled;
        _previousWeather = enabled ? WeatherManager.Weather.SUNNY : _currentWeather;
        _transitionStartTime = Time.time;
    }

    internal bool TryGetRenderMaterials(out Material previous, out Material current,
        out Material transition, out float blend)
    {
        previous = null;
        current = null;
        transition = null;
        blend = 0f;
        if (!_effectsEnabled || _weatherCamera == null)
            return false;

        current = PrepareWeatherMaterial(_currentWeather);
        float progress = _transitionDuration > 0f
            ? (Time.time - _transitionStartTime) / _transitionDuration : 1f;
        if (_previousWeather == _currentWeather || progress >= 1f || _transitionMaterial == null)
        {
            _previousWeather = _currentWeather;
            return current != null;
        }

        previous = PrepareWeatherMaterial(_previousWeather);
        transition = _transitionMaterial;
        blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        return previous != null || current != null;
    }

    private Material PrepareWeatherMaterial(WeatherManager.Weather weather)
    {
        Material material = GetWeatherMaterial(weather);
        if (material != null && weather == WeatherManager.Weather.THUNDERSTORM)
            UpdateStrikeRoots(material);

        return material;
    }

    private Material GetWeatherMaterial(WeatherManager.Weather weather)
    {
        return weather switch
        {
            WeatherManager.Weather.RAIN => _rainMaterial,
            WeatherManager.Weather.THUNDERSTORM => _thunderMaterial,
            WeatherManager.Weather.HEATWAVE => _heatWaveMaterial,
            _ => _sunnyMaterial
        };
    }

    private void UpdateStrikeRoots(Material currentMaterial)
    {
        float now = Time.timeSinceLevelLoad;
        int eventIndex = Mathf.FloorToInt(now * 0.5f);
        if (now < StrikeTime(eventIndex))
            eventIndex--;

        if (_currentStrikeIndex != eventIndex)
        {
            ChooseWorldRoots(_currentStrikeRoots, eventIndex, currentMaterial);
            _currentStrikeIndex = eventIndex;
        }

        currentMaterial.SetVectorArray(_currentStrikeRootsId, _currentStrikeRoots);
    }

    private void ChooseWorldRoots(Vector4[] roots, int eventIndex, Material currentMaterial)
    {
        float halfHeight = _weatherCamera.orthographicSize;
        float height = halfHeight * 2f;
        float width = height * _weatherCamera.aspect;
        Vector3 center = _weatherCamera.transform.position;
        int count = 1 + Mathf.FloorToInt(HashNumber(eventIndex * 23.17f + 37f) * 3f);

        for (int i = 0; i < 3; i++)
        {
            float seed = eventIndex * 31.7f + i * 53.1f;
            float boltHeight = Mathf.Clamp(currentMaterial.GetFloat(_playerWorldHeightId) / height
                * Mathf.Lerp(2f, 2.5f, HashNumber(seed * 3.73f + 5f))
                * currentMaterial.GetFloat(_lightningSizeId), 0.005f, 2.4f);
            float zone = (i + HashNumber(seed * 7.31f + 13f)) / count;
            float margin = Mathf.Min(0.45f, 0.02f + boltHeight * 0.17f / _weatherCamera.aspect);
            float x = Mathf.Lerp(margin, 1f - margin, zone);
            // Thunder는 위쪽 화면 밖에서 시작해 아래로 들어오고,
            // Thunder1은 화면 안쪽 상단에서 시작한다.
            bool outside = currentMaterial.GetFloat(_spawnAboveViewportId) > 0.5f;
            float y = outside
                ? 1f + boltHeight * Mathf.Lerp(0.05f, 0.15f, HashNumber(seed + 17f))
                : Mathf.Lerp(0.91f, 0.98f, HashNumber(seed + 17f));
            roots[i] = new Vector4(center.x + (x - 0.5f) * width,
                center.y + (y - 0.5f) * height, 0f, 0f);
        }
    }

    private static float StrikeTime(int index) => index * 2f + HashNumber(index + 41f);

    private static float HashNumber(float number) =>
        Mathf.Repeat(Mathf.Sin(number * 127.1f) * 43758.5453f, 1f);

}
