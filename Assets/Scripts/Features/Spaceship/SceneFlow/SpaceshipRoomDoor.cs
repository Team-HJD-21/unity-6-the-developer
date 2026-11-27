using System;
using System.Threading.Tasks;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider2D))]
public class SpaceshipRoomDoor : MonoBehaviour
{
    [SerializeField] private string leftRoomSceneName;
    [SerializeField] private string rightSceneName;
    [SerializeField] private bool roomOnRight;

    [SerializeField, Range(0.01f, 0.5f), Tooltip("화면이 어두워졌다 다시 밝아지는 데 걸리는 시간(초)")]
    private float blackoutSeconds = 0.3f;

    private static bool isTransitioning;
    public static bool IsTransitioning => isTransitioning;

    private async void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning || SpaceshipReturnDoor.IsReturning) return;
        if (other.isTrigger || other.gameObject.layer != LayerMask.NameToLayer("PlayerFeetBox") ||
            other.GetComponentInParent<PlayerActorIdentity>() == null) return;
        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null) return;

        isTransitioning = true;
        try
        {
            await ChangeRoomAsync(playerBody);
        }
        catch (Exception error)
        {
            if (this != null) Debug.LogException(error, this);
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private async Task ChangeRoomAsync(Rigidbody2D playerBody)
    {
        string room = roomOnRight ? rightSceneName : leftRoomSceneName;
        Scene spaceship = gameObject.scene;
        await FadeRoomAsync(async () =>
        {
            await LoadRoomAsync(room);
            Scene destination = SceneManager.GetSceneByName(room);
            if (!destination.isLoaded)
                throw new InvalidOperationException("로드된 방 Scene을 찾을 수 없습니다: " + room);

            Transform returnDoor = null;
            foreach (GameObject root in destination.GetRootGameObjects())
            {
                if (root.name != "ToSpaceshipDoor") continue;
                returnDoor = root.transform;
                break;
            }
            if (returnDoor == null)
                throw new InvalidOperationException("방의 ToSpaceshipDoor를 찾을 수 없습니다: " + room);

            GameObject hubRoot = null;
            foreach (GameObject root in spaceship.GetRootGameObjects())
            {
                if (!root.TryGetComponent<SpaceshipHubCompositionRoot>(out _)) continue;
                hubRoot = root;
                break;
            }
            if (hubRoot == null && playerBody != null && playerBody.gameObject.scene == spaceship)
                throw new InvalidOperationException("Player를 소유한 우주선 Hub를 찾지 못해 Scene을 언로드할 수 없습니다.");

            float arrivalX = returnDoor.position.x + (roomOnRight ? 0.3f : -0.3f);
            playerBody.position = new Vector2(arrivalX, playerBody.position.y);
            Physics2D.SyncTransforms();

            if (hubRoot != null) SceneManager.MoveGameObjectToScene(hubRoot, destination);

            AsyncOperation unload = SceneManager.UnloadSceneAsync(spaceship);
            if (unload == null) throw new InvalidOperationException("우주선 Scene 언로드를 시작하지 못했습니다.");
            while (!unload.isDone) await Task.Yield();
        }, blackoutSeconds);
    }

    public static async Task FadeRoomAsync(Func<Task> changeScene, float blackoutSeconds)
    {
        var fadeObject = new GameObject("Room Transition Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        UnityEngine.Object.DontDestroyOnLoad(fadeObject);
        Canvas canvas = fadeObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasGroup group = fadeObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        var imageObject = new GameObject("Blackout", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(fadeObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        imageObject.GetComponent<Image>().color = Color.black;

        float halfDuration = blackoutSeconds * 0.5f;
        try
        {
            await FadeAlphaAsync(group, 0f, 1f, halfDuration);
            try
            {
                await changeScene();
                await Awaitable.NextFrameAsync();
            }
            finally
            {
                await FadeAlphaAsync(group, 1f, 0f, halfDuration);
            }
        }
        finally
        {
            if (fadeObject != null) UnityEngine.Object.Destroy(fadeObject);
        }
    }

    private static async Task FadeAlphaAsync(CanvasGroup group, float from, float to, float duration)
    {
        double start = Time.realtimeSinceStartupAsDouble;
        group.alpha = from;
        while (Time.realtimeSinceStartupAsDouble - start < duration)
        {
            group.alpha = Mathf.Lerp(from, to, (float)((Time.realtimeSinceStartupAsDouble - start) / duration));
            await Awaitable.NextFrameAsync();
        }
        group.alpha = to;
    }

    public static async Task LoadRoomAsync(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            throw new InvalidOperationException("로드할 Scene을 찾을 수 없습니다: " + sceneName);
        if (SceneManager.GetSceneByName(sceneName).isLoaded) return;

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (load == null) throw new InvalidOperationException("Scene 로드를 시작하지 못했습니다: " + sceneName);
        while (!load.isDone) await Task.Yield();
    }

    public static async Task UnLoadRoomAsync(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded) return;

        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
        if (unload == null) throw new InvalidOperationException("Scene 언로드를 시작하지 못했습니다: " + sceneName);
        while (!unload.isDone) await Task.Yield();
    }
}
