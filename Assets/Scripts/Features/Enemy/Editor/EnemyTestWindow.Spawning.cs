using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class EnemyTestWindow
{
    private static readonly int[] TestSpawnCounts = { 1, 5, 10, 50, 100, 500 };

    [SerializeField] private SpawnPoint individualSpawnPoint;
    [SerializeField] private TargetableComponent individualInitialTarget;
    [SerializeField] private EnemyTargetRule individualTargetRule = EnemyTargetRule.PlayerOrTurret;
    [SerializeField] private bool distributeTestSpawns;
    [SerializeField] private List<SpawnPoint> distributionSpawnPoints = new();
    private readonly List<EnemyDefinition> _individualDefinitions = new();
    private readonly List<EnemyTestArea> _testAreas = new();
    private readonly List<(SpawnPoint Point, Toggle Control)> _distributionChoices = new();
    private DropdownField _individualDefinitionField;
    private DropdownField _testAreaField;
    private ObjectField _individualPointField;
    private ObjectField _individualTargetField;
    private Foldout _distributionPointList;
    private Label _spawnAvailability;
    private VisualElement _individualSpawnButtons;

    private void CreateSpawnByType(VisualElement parent)
    {
        parent.Add(new Label("몬스터 종류별 생성"));
        parent.Add(new HelpBox("분산 생성은 출현 가능한 지점을 두 곳 이상 선택하세요. 수량을 지점마다 최대한 균등하게 나눕니다.", HelpBoxMessageType.Info));
        _individualDefinitionField = new DropdownField("몬스터 종류");
        parent.Add(_individualDefinitionField);
        var targetRuleField = new EnumField("타깃 규칙", individualTargetRule);
        targetRuleField.RegisterValueChangedCallback(evt => individualTargetRule = (EnemyTargetRule)evt.newValue);
        parent.Add(targetRuleField);
        _testAreaField = new DropdownField("시험 구역");
        _testAreaField.RegisterValueChangedCallback(_ => SelectTestArea());
        parent.Add(_testAreaField);
        _individualPointField = new ObjectField("출현 지점")
        {
            objectType = typeof(SpawnPoint), allowSceneObjects = true, value = individualSpawnPoint
        };
        _individualPointField.RegisterValueChangedCallback(evt =>
        {
            individualSpawnPoint = evt.newValue as SpawnPoint;
            UpdateStatus();
        });
        parent.Add(_individualPointField);
        var distributeToggle = new Toggle("여러 지점에 분산") { value = distributeTestSpawns };
        distributeToggle.RegisterValueChangedCallback(evt =>
        {
            distributeTestSpawns = evt.newValue;
            if (distributeTestSpawns)
                _distributionPointList.value = true;
            UpdateSpawnPointMode();
            UpdateStatus();
        });
        parent.Add(distributeToggle);
        _distributionPointList = new Foldout { text = "분산 출현 지점", value = true };
        parent.Add(_distributionPointList);
        UpdateSpawnPointMode();
        _individualTargetField = new ObjectField("초기 타깃 · 선택 사항")
        {
            objectType = typeof(TargetableComponent), allowSceneObjects = true, value = individualInitialTarget,
            tooltip = "분산 생성에서 지정하면 해당 타깃까지 경로가 있는 지점만 사용합니다. 비우면 지점마다 도달 가능한 타깃을 찾습니다."
        };
        _individualTargetField.RegisterValueChangedCallback(evt => individualInitialTarget = evt.newValue as TargetableComponent);
        parent.Add(_individualTargetField);
        _spawnAvailability = new Label();
        _spawnAvailability.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(_spawnAvailability);
        _individualSpawnButtons = new VisualElement();
        _individualSpawnButtons.style.flexDirection = FlexDirection.Row;
        _individualSpawnButtons.style.flexWrap = Wrap.Wrap;
        foreach (int count in TestSpawnCounts)
        {
            var button = new Button(() => SpawnSelectedEnemies(count)) { text = $"{count}마리 생성" };
            button.style.flexGrow = 1;
            _individualSpawnButtons.Add(button);
        }
        parent.Add(_individualSpawnButtons);
        parent.Add(new Button(FrameTestArea) { text = "선택 구역을 Scene 뷰에서 보기" });
    }

    /// <summary>새로고침 버튼에서만 정의와 관찰 구역을 조회한다.</summary>
    private void RefreshSingleSpawnOptions()
    {
        if (_individualDefinitionField == null) return;
        string selectedDefinition = _individualDefinitionField.value;
        _individualDefinitions.Clear();
        var names = new List<string>();
        if (binding != null && binding.ExecutionTarget != null && binding.ExecutionTarget.Catalog != null)
        {
            var catalog = new SerializedObject(binding.ExecutionTarget.Catalog);
            var entries = catalog.FindProperty("enemyDefinitions");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var definition = entries.GetArrayElementAtIndex(i).objectReferenceValue as EnemyDefinition;
                if (definition == null || definition.Prefab == null || string.IsNullOrWhiteSpace(definition.EnemyId)) continue;
                _individualDefinitions.Add(definition);
                names.Add(definition.EnemyId);
            }
        }
        _individualDefinitionField.choices = names;
        _individualDefinitionField.SetValueWithoutNotify(names.Contains(selectedDefinition) ? selectedDefinition : names.Count > 0 ? names[0] : "");
        string selectedArea = _testAreaField.value;
        _testAreas.Clear();
        var areaNames = new List<string>();
        if (binding != null && binding.ExecutionTarget != null)
            foreach (var root in binding.ExecutionTarget.gameObject.scene.GetRootGameObjects())
                _testAreas.AddRange(root.GetComponentsInChildren<EnemyTestArea>());
        _testAreas.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var area in _testAreas) areaNames.Add(area.name);
        _testAreaField.choices = areaNames;
        _testAreaField.SetValueWithoutNotify(areaNames.Contains(selectedArea) ? selectedArea : areaNames.Count > 0 ? areaNames[0] : "");
        // 구역 구성 변경으로 선택한 이름이 사라지면 새 구역의 지점·표적을 함께 연결한다.
        if (individualSpawnPoint == null || !areaNames.Contains(selectedArea)) SelectTestArea();
        RefreshDistributionOptions();
    }

    private void UpdateSpawnPointMode()
    {
        _individualPointField.SetEnabled(!distributeTestSpawns);
        _distributionPointList.style.display = distributeTestSpawns ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshDistributionOptions()
    {
        _distributionPointList.Clear();
        _distributionChoices.Clear();
        var points = new List<SpawnPoint>();
        if (EnemyTestSceneTools.IsTestScene(binding))
            foreach (var root in binding.ExecutionTarget.gameObject.scene.GetRootGameObjects())
                points.AddRange(root.GetComponentsInChildren<SpawnPoint>(true));

        distributionSpawnPoints.RemoveAll(point => point == null || !points.Contains(point));
        var choices = new SortedList<string, SpawnPoint>(StringComparer.Ordinal);
        foreach (SpawnPoint point in points)
        {
            string path = AnimationUtility.CalculateTransformPath(point.transform, null);
            string label = choices.ContainsKey(path) ? $"{path} ({point.SpawnPointId})" : path;
            if (!choices.ContainsKey(label))
                choices.Add(label, point);
        }

        foreach (var choice in choices)
        {
            SpawnPoint point = choice.Value;
            var toggle = new Toggle(choice.Key) { value = distributionSpawnPoints.Contains(point) };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    distributionSpawnPoints.Add(point);
                else
                    distributionSpawnPoints.Remove(point);
                UpdateStatus();
            });
            _distributionChoices.Add((point, toggle));
            _distributionPointList.Add(toggle);
        }
        RefreshDistributionAvailability();
    }

    private int RefreshDistributionAvailability()
    {
        int availableCount = 0;
        foreach (var choice in _distributionChoices)
        {
            bool available = choice.Point != null && choice.Point.CanSpawnEnemies &&
                             !string.IsNullOrWhiteSpace(choice.Point.SpawnPointId);
            choice.Control.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (available)
                availableCount++;
            else if (distributionSpawnPoints.Remove(choice.Point))
                choice.Control.SetValueWithoutNotify(false);
        }
        return availableCount;
    }

    private void UpdateSpawnAvailability(bool ready)
    {
        if (_spawnAvailability == null)
            return;

        int availableCount = RefreshDistributionAvailability();
        bool canSpawn = ready && binding.ExecutionTarget != null && binding.ExecutionTarget.CanSpawn;
        bool hasPoints = distributeTestSpawns
            ? distributionSpawnPoints.Count >= 2
            : individualSpawnPoint != null && individualSpawnPoint.CanSpawnEnemies;
        _individualSpawnButtons.SetEnabled(canSpawn && hasPoints && _individualDefinitions.Count > 0);

        if (!EditorApplication.isPlaying)
            _spawnAvailability.text = "EnemyTest 씬을 Play한 뒤 생성할 수 있습니다.";
        else if (!canSpawn)
            _spawnAvailability.text = "EnemyTest의 Host와 Match가 준비되면 생성할 수 있습니다.";
        else if (distributeTestSpawns)
            _spawnAvailability.text = $"선택 {distributionSpawnPoints.Count}곳 / 생성 가능 {availableCount}곳 · 두 곳 이상 선택하세요.";
        else
            _spawnAvailability.text = hasPoints ? string.Empty : "생성 가능한 출현 지점을 선택하세요.";
    }

    private void SelectTestArea()
    {
        var area = _testAreas.Find(item => item != null && item.name == _testAreaField.value);
        if (area == null) return;
        individualSpawnPoint = area.SpawnPoint;
        individualInitialTarget = area.InitialTarget;
        _individualPointField.SetValueWithoutNotify(individualSpawnPoint);
        _individualTargetField.SetValueWithoutNotify(individualInitialTarget);
        selectedTestTarget = individualInitialTarget != null ? individualInitialTarget.GetComponent<EnemyDamageTestTarget>() : null;
        _testTargetField?.SetValueWithoutNotify(selectedTestTarget);
        UpdateStatus();
    }

    private void FrameTestArea()
    {
        var area = _testAreas.Find(item => item != null && item.name == _testAreaField.value);
        if (area != null && SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.Frame(new Bounds(area.transform.position, new Vector3(30f, 22f, 1f)), false);
    }

    private void SpawnSelectedEnemies(int requestedCount)
    {
        try
        {
            var definition = _individualDefinitions.Find(item => item != null && item.EnemyId == _individualDefinitionField.value);
            IReadOnlyList<SpawnPoint> points = distributeTestSpawns ? distributionSpawnPoints : new[] { individualSpawnPoint };
            int spawnedCount = EnemyTestSceneTools.SpawnTestEnemies(binding, definition, points,
                individualInitialTarget, requestedCount, out EnemyController enemy, out string message, individualTargetRule);
            _result.text = message;
            _result.messageType = spawnedCount == requestedCount ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            if (requestedCount == 1 && enemy != null)
            {
                Selection.activeGameObject = enemy.gameObject;
                OnSelectionChanged();
            }
        }
        catch (System.Exception error) { ShowError(error); }
        UpdateStatus();
    }
}
