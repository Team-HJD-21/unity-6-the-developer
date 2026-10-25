// Initial_Stage의 명시적 네트워크 조립과 두 PlayerStart 슬롯을 제작합니다.
// 기존 NGO/Enemy 자산을 재생성하지 않으며 Scene 저장은 CLI 진입점에서만 수행합니다.
using System;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Infrastructure;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class NetworkSceneSetup
    {
        [MenuItem("Tools/TeamHJD/The Developer/Configure Network Scene")]
        public static void InstallOnActiveScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Configure network references outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var root = UnityEngine.Object.FindObjectsByType<InitialStageCompositionRoot>(FindObjectsSortMode.None)
                .Single(item => item.gameObject.scene == scene);
            var manager = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(item => item != null && item.gameObject.scene == scene && item.GetType().FullName == "Unity.Netcode.NetworkManager");
            var binding = root.GetComponent<InitialStageNetworkBinding>();
            if (binding == null) binding = Undo.AddComponent<InitialStageNetworkBinding>(root.gameObject);
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("_manager").objectReferenceValue = manager;
            serialized.FindProperty("_matchAnchorPrefab").objectReferenceValue = EnsureAnchorPrefab();
            serialized.FindProperty("_entry").enumValueIndex = (int)InitialStageNetworkEntry.SinglePlayerHost;
            serialized.ApplyModifiedProperties();
            serialized = new SerializedObject(root);
            serialized.FindProperty("_networkBinding").objectReferenceValue = binding;
            serialized.ApplyModifiedProperties();
            PlayerStart[] starts = UnityEngine.Object.FindObjectsByType<PlayerStart>(FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene == scene).ToArray();
            if (!starts.Any(item => item.SlotIndex == 1))
            {
                PlayerStart first = starts.Single(item => item.SlotIndex == 0);
                var second = new GameObject("PlayerStart_Peer", typeof(PlayerStart));
                Undo.RegisterCreatedObjectUndo(second, "Create peer PlayerStart");
                second.transform.position = first.Position + Vector3.right * 2f;
                second.transform.rotation = first.Rotation;
                serialized = new SerializedObject(second.GetComponent<PlayerStart>());
                serialized.FindProperty("slotIndex").intValue = 1;
                serialized.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            TurretIdentitySceneSetup.InstallOnActiveScene();
        }

        private static GameObject EnsureAnchorPrefab()
        {
            const string path = "Assets/Prefabs/Core/NetworkMatchAnchor.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                if (prefab.GetComponent<NetworkMatchAnchor>() == null || prefab.GetComponent<NetworkObject>() == null)
                    throw new InvalidOperationException("The authored Match anchor prefab is invalid.");
                return prefab;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Core")) AssetDatabase.CreateFolder("Assets/Prefabs", "Core");
            var owner = new GameObject("NetworkMatchAnchor", typeof(NetworkObject), typeof(NetworkMatchAnchor));
            try { return PrefabUtility.SaveAsPrefabAsset(owner, path); }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        public static void ConfigureInitialStage()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
            InstallOnActiveScene();
            if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Failed to save network Scene composition.");
            Debug.Log("NETWORK_SCENE_CONFIGURED");
        }
    }
}
