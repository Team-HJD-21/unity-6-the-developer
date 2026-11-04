using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WeatherShaderApply : MonoBehaviour
{
    private static readonly int CurrentStrikeRootsId = Shader.PropertyToID("_CurrentStrikeRoots");
    private static readonly int PlayerWorldHeightId = Shader.PropertyToID("_PlayerWorldHeight");
    private static readonly int LightningSizeId = Shader.PropertyToID("_LightningSize");
    private static readonly int SpawnAboveViewportId = Shader.PropertyToID("_SpawnAboveViewport");
    private readonly Vector4[] currentStrikeRoots = new Vector4[3];
    private Camera weatherCamera;
    private int currentStrikeIndex = int.MinValue;
    [SerializeField] private Material sunnyMaterial;
    [SerializeField] private Material rainMaterial;
    [SerializeField] private Material thunderMaterial;
    [SerializeField] private Material heatWaveMaterial;
    // 전환 보간용 .mat
    [SerializeField] private Material transitionMaterial;
    // 보간 시간
    [SerializeField, Min(0f)] private float transitionDuration = 1.5f;
    
    private Map.WeatherManager.Weather currentWeather = Map.WeatherManager.Weather.SUNNY;
    private Map.WeatherManager.Weather previousWeather = Map.WeatherManager.Weather.SUNNY;
    private float _transitionStartTime;
    private bool _hasInitialWeather;
    private bool _effectsEnabled = true;
    private static readonly int NextTexId = Shader.PropertyToID("_NextTex");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");

    private void Awake()
    {
        // 잘못된 오브젝트에 붙은 WeatherShaderApply가 추가한 카메라는 화면을 덮지 않게 한다.
        if (CompareTag("MainCamera"))
        {
            weatherCamera = GetComponent<Camera>();
            return;
        }

        Camera attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null)
            attachedCamera.enabled = false;
        enabled = false;
    }

    public void ApplyWeather(Map.WeatherManager.Weather weather)
    {
        if (_hasInitialWeather && currentWeather == weather)
            return;

        // 이전 날씨를 기억하고 전환 시작 시각을 기록한다.
        previousWeather = currentWeather;
        currentWeather = weather;
        _transitionStartTime = Time.time;
        // 처음 설정하는 날씨는 이전 화면이 없으므로 즉시 적용한다.
        if (!_hasInitialWeather)
        {
            previousWeather = weather;
            _hasInitialWeather = true;
        }
        currentStrikeIndex = int.MinValue;
    }

    public void SetEffectsEnabled(bool enabled)
    {
        if (_effectsEnabled == enabled)
            return;

        _effectsEnabled = enabled;
        previousWeather = enabled ? Map.WeatherManager.Weather.SUNNY : currentWeather;
        _transitionStartTime = Time.time;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!_effectsEnabled)
        {
            Graphics.Blit(source, destination);
            return;
        }

        Material currentMaterial = GetWeatherMaterial(currentWeather);
        // 경과 시간 / 전환 시간으로 0~1 진행률을 구한다.
        float progress = transitionDuration > 0f
            ? (Time.time - _transitionStartTime) / transitionDuration : 1f;

        if (previousWeather == currentWeather || progress >= 1f || transitionMaterial == null)
        {
            previousWeather = currentWeather;
            RenderWeather(source, destination, currentMaterial, currentWeather);
            return;
        }

        // 전환 중에는 이전·현재 날씨를 각각 별도 화면에 그린다.
        RenderTexture previousFrame = RenderTexture.GetTemporary(source.descriptor);
        RenderTexture nextFrame = RenderTexture.GetTemporary(source.descriptor);
        try
        {
            RenderWeather(source, previousFrame, GetWeatherMaterial(previousWeather), previousWeather);
            RenderWeather(source, nextFrame, currentMaterial, currentWeather);
            // SmoothStep으로 진행률 양끝을 부드럽게 만든 뒤 두 화면을 섞는다.
            transitionMaterial.SetTexture(NextTexId, nextFrame);
            transitionMaterial.SetFloat(BlendId, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress)));
            Graphics.Blit(previousFrame, destination, transitionMaterial);
        }
        finally
        {
            // 프레임마다 빌린 임시 화면을 반환한다.
            RenderTexture.ReleaseTemporary(previousFrame);
            RenderTexture.ReleaseTemporary(nextFrame);
        }
    }

    private Material GetWeatherMaterial(Map.WeatherManager.Weather weather)
    {
        return weather switch
        {
            Map.WeatherManager.Weather.RAIN => rainMaterial,
            Map.WeatherManager.Weather.THUNDERSTORM => thunderMaterial,
            Map.WeatherManager.Weather.HEATWAVE => heatWaveMaterial,
            _ => sunnyMaterial
        };
    }

    private void RenderWeather(RenderTexture source, RenderTexture destination, Material material,
        Map.WeatherManager.Weather weather)
    {
        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (weather == Map.WeatherManager.Weather.THUNDERSTORM)
            UpdateStrikeRoots(material);

        Graphics.Blit(source, destination, material);
    }

    private void UpdateStrikeRoots(Material currentMaterial)
    {
        float now = Time.timeSinceLevelLoad;
        int eventIndex = Mathf.FloorToInt(now * 0.5f);
        if (now < StrikeTime(eventIndex))
            eventIndex--;

        if (currentStrikeIndex != eventIndex)
        {
            ChooseWorldRoots(currentStrikeRoots, eventIndex, currentMaterial);
            currentStrikeIndex = eventIndex;
        }

        currentMaterial.SetVectorArray(CurrentStrikeRootsId, currentStrikeRoots);
    }

    private void ChooseWorldRoots(Vector4[] roots, int eventIndex, Material currentMaterial)
    {
        float halfHeight = weatherCamera.orthographicSize;
        float height = halfHeight * 2f;
        float width = height * weatherCamera.aspect;
        Vector3 center = weatherCamera.transform.position;
        int count = 1 + Mathf.FloorToInt(HashNumber(eventIndex * 23.17f + 37f) * 3f);

        for (int i = 0; i < 3; i++)
        {
            float seed = eventIndex * 31.7f + i * 53.1f;
            float boltHeight = Mathf.Clamp(currentMaterial.GetFloat(PlayerWorldHeightId) / height
                * Mathf.Lerp(2f, 2.5f, HashNumber(seed * 3.73f + 5f))
                * currentMaterial.GetFloat(LightningSizeId), 0.005f, 2.4f);
            float zone = (i + HashNumber(seed * 7.31f + 13f)) / count;
            float margin = Mathf.Min(0.45f, 0.02f + boltHeight * 0.17f / weatherCamera.aspect);
            float x = Mathf.Lerp(margin, 1f - margin, zone);
            // Thunder는 위쪽 화면 밖에서 시작해 아래로 들어오고,
            // Thunder1은 화면 안쪽 상단에서 시작한다.
            bool outside = currentMaterial.GetFloat(SpawnAboveViewportId) > 0.5f;
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
