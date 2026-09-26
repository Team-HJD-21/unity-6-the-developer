using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class InputSystemUiBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= ReplaceLegacyInputModules;
        SceneManager.sceneLoaded += ReplaceLegacyInputModules;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ReplaceInitialSceneInputModules()
    {
        ReplaceLegacyInputModules(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void ReplaceLegacyInputModules(Scene scene, LoadSceneMode mode)
    {
        StandaloneInputModule[] legacyModules = Object.FindObjectsByType<StandaloneInputModule>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (StandaloneInputModule legacyModule in legacyModules)
        {
            GameObject eventSystemObject = legacyModule.gameObject;
            legacyModule.enabled = false;

            if (!eventSystemObject.TryGetComponent(out InputSystemUIInputModule _))
            {
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            Object.Destroy(legacyModule);
        }
    }
}
