// 공용 Battlefield Grid 기본값을 편집하고 활성 Match의 공간 데이터를 진단합니다.
// Play Mode 오버라이드는 현재 Match에만 적용하며 authoring asset은 변경하지 않습니다.

using System.Collections.Generic;
using System;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Content.Authoring;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Editor
{
    public sealed class BattlefieldDebugWindow : EditorWindow
    {
        private AppRoot _appRoot => BattlefieldDebugSession.AppRoot;
        private BattlefieldSpatialSnapshot _snapshot => BattlefieldDebugSession.Snapshot;
        private BattlefieldGridSettings _gridSettings { get => BattlefieldDebugSession.GridSettings; set => BattlefieldDebugSession.GridSettings = value; }
        private BattlefieldGridConfiguration _runtimeConfiguration;
        private MatchId _runtimeMatchId;
        private string _previewError { get => BattlefieldDebugSession.Error; set => BattlefieldDebugSession.Error = value; }
        [SerializeField] private string _encounterSquadOrder = "Normal";
        [SerializeField] private int _encounterMaxEnemyCount = 10;
        private string _encounterStatus;
        private bool _encounterRequestSucceeded;

        [MenuItem("Tools/TeamHJD/Battlefield Debug")]
        public static void Open()
        {
            var window = GetWindow<BattlefieldDebugWindow>("Battlefield Debug");
            window.minSize = new Vector2(300f, 180f);
        }

        private void OnEnable()
        {
            BattlefieldDebugSession.Changed += OnDiagnosticsChanged;
            BattlefieldDebugSession.Display.IsEnabled = true;
            BattlefieldDebugSession.NotifyDisplayChanged();
            OnDiagnosticsChanged();
        }

        private void OnDisable() => BattlefieldDebugSession.Changed -= OnDiagnosticsChanged;

        private void OnDiagnosticsChanged()
        {
            if (_snapshot == null || _snapshot.MatchId != _runtimeMatchId)
                ResetRuntimeConfiguration();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            DrawAuthoredGridSettings();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Match Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edit Mode는 Core의 동일한 topology 계산으로 터렛 배치 미리보기를 표시하고, Play Mode는 활성 Match snapshot을 표시합니다. 미리보기는 Match를 생성하거나 게임 상태를 변경하지 않습니다.", MessageType.Info);
            EditorGUILayout.HelpBox("Scene View Grid는 Edit Mode에서 공용 기본값, Play Mode에서 활성 Match 설정을 표시합니다. 토폴로지 계산은 Core가 소유하고 이 창은 그 결과만 그립니다.", MessageType.Info);
            var selectedRoot = (AppRoot)EditorGUILayout.ObjectField("App Root", _appRoot, typeof(AppRoot), true);
            if (selectedRoot != _appRoot) BattlefieldDebugSession.Bind(selectedRoot);

            BattlefieldDebugSession.Display.ShowTerritory = EditorGUILayout.Toggle("Territory", BattlefieldDebugSession.Display.ShowTerritory);
            BattlefieldDebugSession.Display.ShowFrontline = EditorGUILayout.Toggle("Frontline", BattlefieldDebugSession.Display.ShowFrontline);
            BattlefieldDebugSession.Display.ShowVertexLabels = EditorGUILayout.Toggle("Vertex labels", BattlefieldDebugSession.Display.ShowVertexLabels);
            BattlefieldDebugSession.Display.ShowGrid = EditorGUILayout.Toggle("Uniform Grid", BattlefieldDebugSession.Display.ShowGrid);
            BattlefieldDebugSession.Display.ShowOccupancy = EditorGUILayout.Toggle("Cell occupancy", BattlefieldDebugSession.Display.ShowOccupancy);

            if (!EditorApplication.isPlaying)
            {
                if (!string.IsNullOrEmpty(_previewError))
                    EditorGUILayout.HelpBox(_previewError, MessageType.Error);
                else if (_snapshot == null || _snapshot.Vertices.Count == 0)
                    EditorGUILayout.HelpBox("활성 Scene에 미리보기 가능한 Turret이 없거나 TurretBattlefieldInputSource가 없습니다. 런타임 자동 갱신을 위해 입력 Source를 Scene에 추가하세요.", MessageType.Warning);
                if (GUILayout.Button("Add Turret Battlefield Input Source to Active Scene"))
                    AddTurretInputSourceToActiveScene();
                if (_snapshot != null)
                    EditorGUILayout.LabelField("Edit Preview / vertices / triangles / frontline",
                        $"{_snapshot.Vertices.Count} / {_snapshot.Triangles.Count} / {_snapshot.FrontlineEdges.Count}");
            }
            else if (_appRoot == null || _snapshot == null)
                EditorGUILayout.HelpBox("연결된 AppRoot에 활성 Match snapshot이 없습니다.", MessageType.None);
            else
            {
                EditorGUILayout.LabelField("Match / revision", $"{_snapshot.MatchId} / {_snapshot.Revision}");
                EditorGUILayout.LabelField("Vertices / triangles / frontline", $"{_snapshot.Vertices.Count} / {_snapshot.Triangles.Count} / {_snapshot.FrontlineEdges.Count}");
                DrawRuntimeGridControls();
                EditorGUILayout.LabelField("Out of bounds (T / P / E)",
                    $"{_snapshot.Grid.OutOfBoundsTurretIds.Count} / {_snapshot.Grid.OutOfBoundsPlayerIds.Count} / {_snapshot.Grid.OutOfBoundsEnemyIds.Count}");
            }

            DrawEncounterControls();

            if (EditorGUI.EndChangeCheck())
            {
                BattlefieldDebugSession.InvalidatePreview();
                BattlefieldDebugSession.NotifyDisplayChanged();
            }
        }

        private void DrawEncounterControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Encounter Request", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "현재 Match의 Battlefield snapshot을 Enemy 측 Encounter 명령 대상으로 전달합니다. 분대 구성은 Squad Preset을 따르고, Grid 점유·적 밀도·Frontline 거리로 사용 가능한 SpawnPoint와 세트 배분을 결정합니다.",
                MessageType.Info);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode에서 Initial_Stage Match를 시작한 뒤 요청할 수 있습니다. 먼저 테스트 런타임을 Active Scene에 구성하세요.", MessageType.None);
                if (GUILayout.Button("Install Encounter Test Runtime in Active Scene"))
                    InstallEncounterTestRuntime();
                return;
            }

            _encounterSquadOrder = EditorGUILayout.TextField("Squad Preset Order", _encounterSquadOrder);
            _encounterMaxEnemyCount = Mathf.Max(1, EditorGUILayout.IntField("Max Enemy Count", _encounterMaxEnemyCount));

            IEncounterDebugCommandTarget target = FindEncounterCommandTarget();
            bool hasSnapshot = _appRoot != null && _snapshot != null && !_snapshot.MatchId.IsEmpty;
            string unavailableReason = null;
            bool ready = target != null && target.CanRequestEncounter(out unavailableReason);
            using (new EditorGUI.DisabledGroupScope(!hasSnapshot || !ready))
            {
                if (GUILayout.Button("Request Encounter from Current Match Snapshot"))
                    _encounterRequestSucceeded = target.TryRequestEncounter(
                        _snapshot, _encounterSquadOrder, _encounterMaxEnemyCount, out _encounterStatus);
            }

            if (!hasSnapshot)
                EditorGUILayout.HelpBox("활성 AppRoot/Match Battlefield snapshot이 없습니다. Initial_Stage에서 Match가 먼저 시작되어야 합니다.", MessageType.Warning);
            else if (target == null)
                EditorGUILayout.HelpBox("Active Scene에 IEncounterDebugCommandTarget 구현체가 없습니다. 테스트 런타임 설치 후 Play Mode를 다시 시작하세요.", MessageType.Warning);
            else if (!ready)
                EditorGUILayout.HelpBox(unavailableReason, MessageType.Warning);

            if (!string.IsNullOrEmpty(_encounterStatus))
                EditorGUILayout.HelpBox(_encounterStatus,
                    _encounterRequestSucceeded ? MessageType.Info : MessageType.Warning);
        }

        private IEncounterDebugCommandTarget FindEncounterCommandTarget()
        {
            // NGO moves NetworkManager roots to DontDestroyOnLoad when networking starts.
            // The debug command target lives on that same root, so it may no longer belong
            // to the gameplay scene even though it is the active Match's valid target.
            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component != null && component.isActiveAndEnabled &&
                    component is IEncounterDebugCommandTarget target)
                    return target;
            return null;
        }

        private AppRoot FindActiveSceneAppRoot()
        {
            return FindAnyObjectByType<AppRoot>();
        }

        private void InstallEncounterTestRuntime()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                _encounterStatus = "Loaded Active Scene이 없습니다.";
                return;
            }

            Type networkManagerType = FindLoadedComponentType("Unity.Netcode.NetworkManager");
            Type unityTransportType = FindLoadedComponentType("Unity.Netcode.Transports.UTP.UnityTransport");
            Type networkObjectType = FindLoadedComponentType("Unity.Netcode.NetworkObject");
            Type executorType = FindLoadedComponentType("EnemySpawnExecutor");
            Type spawnPointType = FindLoadedComponentType("SpawnPoint");
            Type launcherType = FindLoadedComponentType("EnemyNetworkTestLauncher");
            if (networkManagerType == null || unityTransportType == null || networkObjectType == null ||
                executorType == null || spawnPointType == null || launcherType == null)
            {
                _encounterStatus = "Enemy/Netcode 스크립트가 컴파일된 뒤 다시 시도하세요. Console 컴파일 오류도 확인하세요.";
                return;
            }

            UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/PoC/Enemy/Data/Spawning/EnemyCatalog.asset");
            UnityEngine.Object networkPrefabs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/DefaultNetworkPrefabs.asset");
            if (catalog == null || networkPrefabs == null)
            {
                _encounterStatus = "EnemyCatalog 또는 DefaultNetworkPrefabs 에셋을 찾지 못했습니다.";
                return;
            }

            GameObject networkObject = FindComponentInScene(activeScene, networkManagerType);
            bool createdNetworkManager = networkObject == null;
            if (createdNetworkManager)
            {
                networkObject = new GameObject("Encounter Debug Network");
                Undo.RegisterCreatedObjectUndo(networkObject, "Install Encounter Test Runtime");
                SceneManager.MoveGameObjectToScene(networkObject, activeScene);
                networkObject.AddComponent(unityTransportType);
                networkObject.AddComponent(networkManagerType);
            }
            else if (networkObject.GetComponent(unityTransportType) == null)
            {
                Undo.AddComponent(networkObject, unityTransportType);
            }

            Component networkManagerComponent = networkObject.GetComponent(networkManagerType);
            Undo.RecordObject(networkManagerComponent, "Configure Encounter Test Runtime");
            var networkManagerSerialized = new SerializedObject(networkManagerComponent);
            SerializedProperty transportProperty = networkManagerSerialized.FindProperty("NetworkConfig.NetworkTransport");
            if (transportProperty != null)
                transportProperty.objectReferenceValue = networkObject.GetComponent(unityTransportType);
            SerializedProperty networkPrefabLists = networkManagerSerialized.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
            if (networkPrefabLists != null && networkPrefabLists.isArray)
            {
                bool alreadyRegistered = false;
                for (int index = 0; index < networkPrefabLists.arraySize; index++)
                    alreadyRegistered |= networkPrefabLists.GetArrayElementAtIndex(index).objectReferenceValue == networkPrefabs;
                if (!alreadyRegistered)
                {
                    int index = networkPrefabLists.arraySize;
                    networkPrefabLists.arraySize++;
                    networkPrefabLists.GetArrayElementAtIndex(index).objectReferenceValue = networkPrefabs;
                }
            }
            networkManagerSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(networkManagerComponent);

            GameObject spawner = FindComponentInScene(activeScene, executorType);
            if (spawner == null)
            {
                spawner = new GameObject("Encounter Debug Spawner");
                Undo.RegisterCreatedObjectUndo(spawner, "Install Encounter Test Runtime");
                SceneManager.MoveGameObjectToScene(spawner, activeScene);
                spawner.AddComponent(networkObjectType);
                Component executor = spawner.AddComponent(executorType);
                var executorSerialized = new SerializedObject(executor);
                SerializedProperty catalogProperty = executorSerialized.FindProperty("enemyList");
                if (catalogProperty != null) catalogProperty.objectReferenceValue = catalog;
                executorSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(executor);
            }
            else if (spawner.GetComponent(networkObjectType) == null)
            {
                Undo.AddComponent(spawner, networkObjectType);
            }

            Type playerStartType = FindLoadedComponentType("TeamHJD.Game.Presentation.Scene.PlayerStart");
            GameObject playerStartObject = playerStartType == null
                ? null
                : FindComponentInScene(activeScene, playerStartType);
            BattlefieldGridConfiguration spawnGrid = _gridSettings != null
                ? _gridSettings.CreateConfiguration()
                : BattlefieldGridConfiguration.Default;
            Vector3 spawnAnchor = playerStartObject != null
                ? playerStartObject.transform.position
                : new Vector3(
                    (float)spawnGrid.Origin.X,
                    (float)spawnGrid.Origin.Y,
                    (float)spawnGrid.OriginZ);
            EnsureSpawnPoint(spawner.transform, spawnPointType,
                "Encounter Spawn Left", "EncounterLeft", -1, spawnAnchor);
            EnsureSpawnPoint(spawner.transform, spawnPointType,
                "Encounter Spawn Right", "EncounterRight", 1, spawnAnchor);

            if (networkObject.GetComponent(launcherType) == null)
                Undo.AddComponent(networkObject, launcherType);

            _encounterStatus = "Encounter 테스트 런타임을 구성했습니다. Scene 저장 후 Play Mode에서 Match를 시작하세요. " +
                               "NGO 기본 Prefab 목록과 Squad Preset Normal을 사용합니다.";
            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = networkObject;
        }

        private void EnsureSpawnPoint(
            Transform parent,
            Type spawnPointType,
            string objectName,
            string id,
            int side,
            Vector3 anchor)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null && existing.GetComponent(spawnPointType) != null) return;

            var pointObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(pointObject, "Install Encounter Test Runtime");
            pointObject.transform.SetParent(parent, false);
            BattlefieldGridConfiguration configuration = _gridSettings != null
                ? _gridSettings.CreateConfiguration()
                : BattlefieldGridConfiguration.Default;
            float offset = Mathf.Min(8f, (float)Math.Min(configuration.MapWidth, configuration.MapHeight) * 0.2f);
            float insetX = (float)Math.Min(configuration.CellWidth, configuration.MapWidth * 0.1d);
            float worldX = Mathf.Clamp(
                anchor.x + side * offset,
                (float)configuration.MinimumX + insetX,
                (float)configuration.MaximumX - insetX);
            pointObject.transform.position = new Vector3(
                worldX,
                anchor.y,
                (float)configuration.OriginZ);
            Component spawnPoint = pointObject.AddComponent(spawnPointType);
            var serialized = new SerializedObject(spawnPoint);
            SerializedProperty idProperty = serialized.FindProperty("spawnPointId");
            SerializedProperty areaProperty = serialized.FindProperty("areaId");
            if (idProperty != null) idProperty.stringValue = id;
            if (areaProperty != null) areaProperty.stringValue = "Debug";
            serialized.ApplyModifiedProperties();
        }

        private static Type FindLoadedComponentType(string fullTypeName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullTypeName, false);
                if (type != null && typeof(Component).IsAssignableFrom(type)) return type;
            }
            return null;
        }

        private static GameObject FindComponentInScene(Scene scene, Type componentType)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Component component in root.GetComponentsInChildren(componentType, true))
                    if (component != null) return component.gameObject;
            return null;
        }

        private void DrawAuthoredGridSettings()
        {
            EditorGUILayout.LabelField("Authored Grid Defaults", EditorStyles.boldLabel);
            _gridSettings = (BattlefieldGridSettings)EditorGUILayout.ObjectField(
                "Shared Settings Asset", _gridSettings, typeof(BattlefieldGridSettings), false);
            if (_gridSettings == null)
            {
                EditorGUILayout.HelpBox("공용 Grid 기본값 에셋을 지정하세요. Scene composition은 이 값을 새 Match에 적용합니다.", MessageType.Warning);
                return;
            }

            var serializedSettings = new SerializedObject(_gridSettings);
            serializedSettings.Update();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_originX"), new GUIContent("Center X"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_originY"), new GUIContent("Center Y"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_originZ"), new GUIContent("Origin Z (draw only)"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_mapWidth"), new GUIContent("Map Width"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_mapHeight"), new GUIContent("Map Height"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_cellsX"), new GUIContent("Cells X"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_cellsY"), new GUIContent("Cells Y"));
            serializedSettings.ApplyModifiedProperties();
            try
            {
                _gridSettings.CreateConfiguration();
            }
            catch (System.ArgumentException exception)
            {
                EditorGUILayout.HelpBox($"저장된 설정이 유효하지 않습니다: {exception.Message}", MessageType.Error);
            }

            EditorGUILayout.HelpBox("이 값은 저장되어 이후 생성되는 Match의 기본값이 됩니다. 활성 Match에는 자동 적용되지 않습니다.", MessageType.Info);
        }

        private void ResetRuntimeConfiguration()
        {
            _runtimeMatchId = _snapshot == null ? default : _snapshot.MatchId;
            _runtimeConfiguration = _snapshot?.Grid.Configuration;
        }

        private void DrawRuntimeGridControls()
        {
            EditorGUILayout.LabelField("Runtime Grid Override (active Match only)", EditorStyles.boldLabel);
            if (_runtimeConfiguration == null)
            {
                EditorGUILayout.HelpBox("현재 Match의 Grid 설정을 읽을 수 없습니다.", MessageType.Warning);
                return;
            }

            var originX = EditorGUILayout.DoubleField("Center X", _runtimeConfiguration.Origin.X);
            var originY = EditorGUILayout.DoubleField("Center Y", _runtimeConfiguration.Origin.Y);
            var originZ = EditorGUILayout.DoubleField("Origin Z (draw only)", _runtimeConfiguration.OriginZ);
            var mapWidth = EditorGUILayout.DoubleField("Map Width", _runtimeConfiguration.MapWidth);
            var mapHeight = EditorGUILayout.DoubleField("Map Height", _runtimeConfiguration.MapHeight);
            var cellsX = EditorGUILayout.IntField("Cells X", _runtimeConfiguration.CellsX);
            var cellsY = EditorGUILayout.IntField("Cells Y", _runtimeConfiguration.CellsY);

            try
            {
                _runtimeConfiguration = new BattlefieldGridConfiguration(
                    new BattlefieldPoint(originX, originY), originZ, mapWidth, mapHeight, cellsX, cellsY);
            }
            catch (System.ArgumentException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply to Active Match"))
                _appRoot.ReconfigureBattlefieldGrid(_runtimeConfiguration);
            if (GUILayout.Button("Restore Authored Defaults"))
            {
                if (_gridSettings == null)
                    EditorGUILayout.HelpBox("먼저 공용 Grid 설정 에셋을 지정하세요.", MessageType.Warning);
                else
                {
                    try
                    {
                        _runtimeConfiguration = _gridSettings.CreateConfiguration();
                        _appRoot.ReconfigureBattlefieldGrid(_runtimeConfiguration);
                    }
                    catch (System.ArgumentException exception)
                    {
                        EditorGUILayout.HelpBox($"저장된 설정이 유효하지 않습니다: {exception.Message}", MessageType.Error);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("여기서 적용한 값은 현재 Match에서만 유지됩니다. 공유 설정 에셋에 저장되지 않습니다.", MessageType.Warning);
        }

        private void AddTurretInputSourceToActiveScene()
        {
            const string typeName = "TeamHJD.Game.Turrets.TurretBattlefieldInputSource";
            Type componentType = FindLoadedType(typeName);
            if (componentType == null)
            {
                _previewError = "TurretBattlefieldInputSource is not loaded yet. Wait for Unity script compilation and check the Console for TeamHJD.Game.Turrets errors.";
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                _previewError = "No loaded active Scene is available.";
                return;
            }

            var sourceObject = new GameObject("Turret Battlefield Input Source");
            Undo.RegisterCreatedObjectUndo(sourceObject, "Add Turret Battlefield Input Source");
            SceneManager.MoveGameObjectToScene(sourceObject, activeScene);
            sourceObject.AddComponent(componentType);
            Selection.activeGameObject = sourceObject;
            _previewError = null;
            BattlefieldDebugSession.InvalidatePreview();
        }

        private static Type FindLoadedType(string fullTypeName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullTypeName, false);
                if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type)) return type;
            }
            return null;
        }

    }
}
