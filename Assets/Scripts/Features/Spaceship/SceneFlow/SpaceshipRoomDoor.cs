using System;
using System.Threading.Tasks;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
public class SpaceshipRoomDoor : MonoBehaviour
{
    [SerializeField] private string destinationScenePath;
    [SerializeField] private string destinationDoorName = "LeftDoor";
    [SerializeField] private Vector2 arrivalOffset = new(1.5f, 0f);

    private static bool isTransitioning;
    private static float nextTransitionTime;

    private async void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning || Time.time < nextTransitionTime) return;
        var identity = other.GetComponentInParent<PlayerActorIdentity>();
        if (identity == null) return;

        isTransitioning = true;
        try
        {
            await TravelAsync(identity.transform);
            nextTransitionTime = Time.time + 0.5f;
        }
        catch (Exception error)
        {
            Debug.LogException(error, this);
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private async Task TravelAsync(Transform player)
    {
        if (string.IsNullOrWhiteSpace(destinationScenePath) ||
            SceneUtility.GetBuildIndexByScenePath(destinationScenePath) < 0)
            throw new InvalidOperationException("목적지 Scene을 Build Settings에서 찾을 수 없습니다: " + destinationScenePath);

        Scene source = gameObject.scene;
        Scene destination = SceneManager.GetSceneByPath(destinationScenePath);
        if (!destination.isLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(destinationScenePath, LoadSceneMode.Additive);
            if (load == null) throw new InvalidOperationException("목적지 Scene 로드를 시작하지 못했습니다: " + destinationScenePath);
            while (!load.isDone) await Task.Yield();
            destination = SceneManager.GetSceneByPath(destinationScenePath);
        }

        Transform destinationDoor = FindDoor(destination);
        if (destinationDoor == null)
            throw new InvalidOperationException("목적지 문을 찾을 수 없습니다: " + destinationDoorName);

        Vector3 position = destinationDoor.position;
        player.position = new Vector3(position.x + arrivalOffset.x, position.y + arrivalOffset.y, player.position.z);
        Physics2D.SyncTransforms();
        SetSceneCamera(source, false);
        SetSceneCamera(destination, true);
        SceneManager.SetActiveScene(destination);
    }

    private Transform FindDoor(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == destinationDoorName) return child;
            }
        }
        return null;
    }

    private static void SetSceneCamera(Scene scene, bool enabled)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = enabled;
                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = enabled;
            }
        }
    }
}
