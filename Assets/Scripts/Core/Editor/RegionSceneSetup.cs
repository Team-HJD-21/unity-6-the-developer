// 현재 Scene의 명시적 CU/터렛 컨트롤러를 지역 바인딩에 연결하는 제작 도구입니다.
// 기존 Scene/Prefab 자산은 이동하지 않으며 CLI 진입점은 Initial_Stage만 저장합니다.
using System;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.FieldTurrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class RegionSceneSetup
    {
        [MenuItem("Tools/TeamHJD/The Developer/Configure Region Binding")]
        public static void InstallOnActiveScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Configure scene references outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = UnityEngine.Object.FindObjectsByType<InitialStageCompositionRoot>(FindObjectsSortMode.None)
                .Where(root => root.gameObject.scene == scene).ToArray();
            var controllers = UnityEngine.Object.FindObjectsByType<FieldTurretController>(FindObjectsSortMode.None)
                .Where(controller => controller.gameObject.scene == scene).ToArray();
            var units = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(component => component != null && component.gameObject.scene == scene &&
                    component.GetType().Name == "ControlUnitHealth").ToArray();
            if (roots.Length != 1 || controllers.Length != 1 || units.Length != 1)
                throw new InvalidOperationException("Expected one active InitialStageCompositionRoot, FieldTurretController and CU.");
            RegionSceneBinding binding = roots[0].GetComponent<RegionSceneBinding>();
            if (binding == null) binding = Undo.AddComponent<RegionSceneBinding>(roots[0].gameObject);
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("_controller").objectReferenceValue = controllers[0];
            serialized.FindProperty("_controlUnit").objectReferenceValue = units[0].transform;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void ConfigureInitialStage()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
            InstallOnActiveScene();
            if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Failed to save Initial_Stage region binding.");
            Debug.Log("REGION_BINDING_CONFIGURED");
        }
    }
}
