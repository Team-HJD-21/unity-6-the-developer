// 같은 Scene의 터렛 배치마다 영구 제작 키를 부여하고 Match 대응표 조립을 연결합니다.
// 명시적 제작 작업에서만 누락/중복 키를 고치며 기존 정상 키·Prefab GUID는 유지합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace TeamHJD.Game.Editor
{
    public static class TurretIdentitySceneSetup
    {
        [MenuItem("Tools/TeamHJD/The Developer/Configure Turret Identities")]
        public static void InstallOnActiveScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Configure authored IDs outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var root = UnityEngine.Object.FindObjectsByType<InitialStageCompositionRoot>(FindObjectsSortMode.None).Single(item => item.gameObject.scene == scene);
            var controller = UnityEngine.Object.FindObjectsByType<TurretController>(FindObjectsSortMode.None).Single(item => item.gameObject.scene == scene);
            var source = UnityEngine.Object.FindObjectsByType<TurretBattlefieldInputSource>(FindObjectsSortMode.None).Single(item => item.gameObject.scene == scene);
            var binding = root.GetComponent<TurretMatchBinding>();
            if (binding == null) binding = Undo.AddComponent<TurretMatchBinding>(root.gameObject);
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("_controller").objectReferenceValue = controller;
            serialized.FindProperty("_battlefieldSource").objectReferenceValue = source;
            serialized.FindProperty("_commandLevelCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TurretLevelUpgradeCatalog>("Assets/Data/Turrets/TurretLevelUpgrade_Stage1.asset");
            var upgradeAssets = AssetDatabase.FindAssets("t:TurretUpgradeDefinition", new[] { "Assets/Data/Turrets" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TurretUpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(asset => asset.Id, StringComparer.Ordinal).ToArray();
            SerializedProperty upgrades = serialized.FindProperty("_commandUpgrades"); upgrades.arraySize = upgradeAssets.Length;
            for (int index = 0; index < upgradeAssets.Length; index++) upgrades.GetArrayElementAtIndex(index).objectReferenceValue = upgradeAssets[index];
            serialized.ApplyModifiedProperties();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TurretBase turret in scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<TurretBase>(true)))
            {
                var identity = turret.GetComponent<SceneEntityIdentity>();
                if (identity == null) identity = Undo.AddComponent<SceneEntityIdentity>(turret.gameObject);
                string key = identity.EntityId.Value;
                if (!string.IsNullOrWhiteSpace(key) && ids.Add(key)) continue;
                serialized = new SerializedObject(identity);
                string next = "turret/" + Guid.NewGuid().ToString("N"); ids.Add(next);
                serialized.FindProperty("_entityId").stringValue = next;
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(identity);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
