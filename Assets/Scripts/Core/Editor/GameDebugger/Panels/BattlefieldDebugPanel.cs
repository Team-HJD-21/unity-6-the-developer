// Game Debugger 내부에서 Grid 설정·Scene 구성·Encounter 요청 UI를 제공합니다.
// Play Mode 오버라이드는 현재 Match에만 적용하며 authoring asset은 변경하지 않습니다.
// 독립 EditorWindow를 만들지 않고 소유 창이 구독 수명을 명시적으로 관리합니다.

using System.Collections.Generic;
using System;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Content.Authoring;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class BattlefieldDebugPanel : IDisposable
    {
        private AppRoot _appRoot => BattlefieldDebugSession.AppRoot;
        private BattlefieldSpatialSnapshot _snapshot => BattlefieldDebugSession.Snapshot;
        private BattlefieldGridSettings _gridSettings { get => BattlefieldDebugSession.GridSettings; set => BattlefieldDebugSession.GridSettings = value; }
        private BattlefieldGridConfiguration _runtimeConfiguration;
        private MatchId _runtimeMatchId;
        private string _previewError { get => BattlefieldDebugSession.Error; set => BattlefieldDebugSession.Error = value; }
        [SerializeField] private string _encounterSquadOrder = "Normal";
        [SerializeField] private int _encounterMaxEnemyCount = 10;
        [SerializeField] private bool _showLayers=true, _showCell, _showAuthored, _showRuntime, _showSetup, _showNotes, _showLastCommand;
        [SerializeField] private Vector2 _cellContentsScroll;
        [SerializeField] private EncounterOriginDebugControls _originControls = new EncounterOriginDebugControls();
        private string _encounterStatus;
        private bool _encounterRequestSucceeded;
        [NonSerialized] private bool _isAttached;

        public void Attach()
        {
            if (_isAttached) return;
            _isAttached = true;
            BattlefieldDebugSession.Changed += OnDiagnosticsChanged;
            OnDiagnosticsChanged();
        }

        public void Dispose()
        {
            EncounterRepeatDebugControls.Release(this);
            if (!_isAttached) return;
            BattlefieldDebugSession.Changed -= OnDiagnosticsChanged;
            _isAttached = false;
        }

        private void OnDiagnosticsChanged()
        {
            if (_snapshot == null || _snapshot.MatchId != _runtimeMatchId)
            {
                ResetRuntimeConfiguration();
                _encounterStatus = null;
                _encounterRequestSucceeded = false;
            }
        }

        public void Draw(int tab)
        {
            EditorGUI.BeginChangeCheck();
            if (tab == 1)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) BattlefieldDiagnosticPanels.DrawOverview();
                if (!BattlefieldDebugSession.Display.IsCaptureMode) RegionDebugControls.Draw();
                FrontlineInspector.Draw();
                _showLayers=EditorGUILayout.Foldout(_showLayers,"Scene 표시 / 레이어",true);
                if (_showLayers) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) BattlefieldDiagnosticPanels.DrawDisplayControls();
                _showCell=EditorGUILayout.Foldout(_showCell,"선택 셀 / 범위 초과 개체",true);
                if (_showCell) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) BattlefieldDiagnosticPanels.DrawCellDetails(ref _cellContentsScroll);
                if (!BattlefieldDebugSession.Display.IsCaptureMode)
                {
                    _showAuthored=EditorGUILayout.Foldout(_showAuthored,"공용 Grid 기본값 / 에셋",true);
                    if (_showAuthored) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) DrawAuthoredGridSettings();
                    if (EditorApplication.isPlaying && _appRoot != null && _snapshot != null)
                    {
                        _showRuntime=EditorGUILayout.Foldout(_showRuntime,"현재 Match Grid override / 명시적 적용",true);
                        if (_showRuntime) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) DrawRuntimeGridControls();
                    }
                    if (!EditorApplication.isPlaying)
                    {
                        _showSetup=EditorGUILayout.Foldout(_showSetup,"Scene 구성 도구",true);
                        if (_showSetup && GUILayout.Button("Add Turret Battlefield Input Source to Active Scene")) AddTurretInputSourceToActiveScene();
                    }
                }
            }
            else if (tab == 2)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) BattlefieldDiagnosticPanels.DrawOverview();
                if (!BattlefieldDebugSession.Display.IsCaptureMode) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) DrawEncounterControls();
                _showNotes=EditorGUILayout.Foldout(_showNotes,"요청 계약 / 설명",true);
                if (_showNotes) EditorGUILayout.HelpBox("명령은 현재 Match snapshot을 전달합니다. 실제 공간 판단·지점 배정과 생성 위치는 Statistics > Spawn decisions에서 조회합니다. 공식 PlanId/idempotency token은 별도 계약입니다.",MessageType.None);
            }
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

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Stage_1의 정식 Match는 자동 생산을 시작합니다. 아래 설치는 별도 시험 Scene을 구성할 때만 사용하세요.", MessageType.None);
                if (GUILayout.Button("Install Encounter Test Runtime in Active Scene"))
                    InstallEncounterTestRuntime();
                return;
            }

            var scene = UnityEngine.Object.FindFirstObjectByType<InitialStageCompositionRoot>();
            var production = scene != null ? scene.MatchCoordinator?.EncounterProduction : null;
            if (production != null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Match 정규 생산", production.IsRunning ? "RUNNING" : "STOPPED", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("다음 요청 / 간격", $"{production.RemainingSeconds:F1} / {production.IntervalSeconds:F1} 게임 초");
                    EditorGUILayout.LabelField("요청 회차 / 마지막 결과", $"{production.AttemptedRequests} / {production.LastResult?.Status.ToString() ?? "대기"}");
                    if (scene.CaptureTrend.HasHistory)
                        EditorGUILayout.LabelField("관측 기간 / 점유 순변화", $"{scene.CaptureTrend.ObservedSeconds:F0}초 / {scene.CaptureTrend.NetRatio:P1} · 증가 {scene.CaptureTrend.PositiveChanges}회");
                    if (scene.FieldTime != null)
                        EditorGUILayout.LabelField("공용 / Field 시각", $"{scene.MatchTime.ElapsedSeconds:F1} / {scene.FieldTime.ElapsedSeconds:F1}초 · {(scene.FieldTime.Snapshot.IsPaused ? "Field 보류" : "진행")}");
                }
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
                EditorGUILayout.HelpBox("활성 AppRoot/Match Battlefield snapshot이 없습니다. Stage_1에서 Match가 먼저 시작되어야 합니다.", MessageType.Warning);
            else if (target == null)
                EditorGUILayout.HelpBox("Active Scene에 IEncounterDebugCommandTarget 구현체가 없습니다. 테스트 런타임 설치 후 Play Mode를 다시 시작하세요.", MessageType.Warning);
            else if (!ready)
                EditorGUILayout.HelpBox(unavailableReason, MessageType.Warning);

            if (!string.IsNullOrEmpty(_encounterStatus))
            {
                if (!_encounterRequestSucceeded)
                    EditorGUILayout.HelpBox(_encounterStatus, MessageType.Warning);
                else
                {
                    EditorGUILayout.LabelField("요청 성공 · 수량과 생성 위치는 요청 표 / Statistics에서 확인", EditorStyles.miniLabel);
                    _showLastCommand = EditorGUILayout.Foldout(_showLastCommand, "마지막 명령 상세", true);
                    if (_showLastCommand) EditorGUILayout.HelpBox(_encounterStatus, MessageType.None);
                }
            }
            EncounterRepeatDebugControls.Draw(this, _encounterSquadOrder, _encounterMaxEnemyCount, target);
            _originControls.Draw(target as IEncounterOriginRequestTarget, ready, _encounterSquadOrder, _encounterMaxEnemyCount);
            if (target is UnityEngine.Object owner && target is IGameDiagnosticsSource diagnostics && diagnostics.IsServer)
            {
                var serialized = new SerializedObject(owner);
                var policy = serialized.FindProperty("additionalSpawns");
                if (policy != null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.PropertyField(policy, new GUIContent("상황 기반 추가 출현 · 현재 Match"), true);
                        if (serialized.ApplyModifiedPropertiesWithoutUndo())
                            EditorGUILayout.HelpBox("다음 자동 계획부터 적용됩니다. 이미 예고된 편성·시간은 바꾸지 않습니다.", MessageType.Info);
                    }
                }
            }
        }

        private IEncounterDebugCommandTarget FindEncounterCommandTarget()
        {
            // NGO 루트는 DDOL로 이동하므로 활성 오브젝트 전체에서 명령 대상을 조회합니다.
            foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component != null && component.isActiveAndEnabled &&
                    component is IEncounterDebugCommandTarget target)
                    return target;
            return null;
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
            Type launcherType = FindLoadedComponentType("EncounterSceneBinding");
            if (networkManagerType == null || unityTransportType == null || networkObjectType == null ||
                executorType == null || spawnPointType == null || launcherType == null)
            {
                _encounterStatus = "Enemy/Netcode 스크립트가 컴파일된 뒤 다시 시도하세요. Console 컴파일 오류도 확인하세요.";
                return;
            }

            UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/Data/Enemies/Spawning/EnemyCatalog.asset");
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
            const string typeName = "TeamHJD.Game.FieldTurrets.FieldTurretBattlefieldInputSource";
            Type componentType = FindLoadedType(typeName);
            if (componentType == null)
            {
                _previewError = "FieldTurretBattlefieldInputSource is not loaded yet. Wait for Unity script compilation and check the Console for TeamHJD.Game.FieldTurrets errors.";
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
