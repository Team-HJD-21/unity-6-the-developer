using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ShaderTests : MonoBehaviour
{
    private static readonly int CurrentStrikeRootsId = Shader.PropertyToID("_CurrentStrikeRoots");
    private static readonly int PlayerWorldHeightId = Shader.PropertyToID("_PlayerWorldHeight");
    private static readonly int LightningSizeId = Shader.PropertyToID("_LightningSize");
    private static readonly int SpawnAboveViewportId = Shader.PropertyToID("_SpawnAboveViewport");
    private readonly Vector4[] currentStrikeRoots = new Vector4[3];
    private Camera weatherCamera;
    private int currentStrikeIndex = int.MinValue;
    // 기본 상태는 두 플래그가 모두 false다.
    [SerializeField] private Material sunnyMaterial;
    [SerializeField] private Material rainMaterial;
    [SerializeField] private Material thunderMaterial;
    [SerializeField] private Material thunder1Material;
    [SerializeField] private Material heatWaveMaterial;

    public bool isRain { get; private set; } = false;
    public bool isThunder { get; private set; } = false;
    public bool isThunder1 { get; private set; } = false;
    public bool isHeatWave { get; private set; } = false;

    private void Awake()
    {
        // 잘못된 오브젝트에 붙은 ShaderTests가 추가한 카메라는 화면을 덮지 않게 한다.
        if (CompareTag("MainCamera"))
        {
            weatherCamera = GetComponent<Camera>();
            return;
        }

        Camera attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null)
            attachedCamera.enabled = false;
        enabled = false;
        Debug.LogWarning("ShaderTests는 Main Camera에만 붙여야 합니다.", this);
    }

    /*
    private void OnEnable()
    {
        isRain = false;
        isThunder = false;
        InvokeRepeating(nameof(ChangeTestWeather), 5f, 5f);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ChangeTestWeather));
    }

    private void ChangeTestWeather()
    {
        if (isThunder)
        {
            isThunder = false;
        }
        else if (isRain)
        {
            isRain = false;
            isThunder = true;
        }
        else
        {
            isRain = true;
        }
    }
*/
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Material currentMaterial = isRain ? rainMaterial
            : isThunder ? thunderMaterial
            : isThunder1 ? thunder1Material
            : isHeatWave ? heatWaveMaterial : sunnyMaterial;

        if (currentMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (isThunder || isThunder1)
            UpdateStrikeRoots(currentMaterial);

        Graphics.Blit(source, destination, currentMaterial);
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

    public void ChangeWeatherFlag()
    {
        if (isHeatWave)
        {
            isHeatWave = false;
        }
        else if (isThunder1)
        {
            isThunder1 = false;
            isHeatWave = true;
        }
        else if (isThunder)
        {
            isThunder = false;
            isThunder1 = true;
        }
        else if (isRain)
        {
            isRain = false;
            isThunder = true;
        }
        else
        {
            isRain = true;
        }

        currentStrikeIndex = int.MinValue;
    }
}
