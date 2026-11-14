using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class EnemyTestWindow
{
    [SerializeField] private SpawnPoint individualSpawnPoint;
    [SerializeField] private TargetableComponent individualInitialTarget;
    private readonly List<EnemyDefinition> _individualDefinitions = new();
    private readonly List<EnemyTestArea> _testAreas = new();
    private DropdownField _individualDefinitionField;
    private DropdownField _testAreaField;
    private ObjectField _individualPointField;
    private ObjectField _individualTargetField;
    private Button _individualSpawnButton;

    private void CreateSingleSpawn(VisualElement parent)
    {
        parent.Add(new Label("개별 몬스터 기능 시험"));
        parent.Add(new HelpBox("예산·편성 선택을 생략하고 지정 몬스터를 1마리 생성합니다. Encounter 전체 경로는 위의 편성 요청 버튼으로 검사하세요.", HelpBoxMessageType.Info));
        _individualDefinitionField = new DropdownField("몬스터 종류");
        parent.Add(_individualDefinitionField);
        _testAreaField = new DropdownField("시험 구역");
        _testAreaField.RegisterValueChangedCallback(_ => SelectTestArea());
        parent.Add(_testAreaField);
        _individualPointField = new ObjectField("출현 지점")
        {
            objectType = typeof(SpawnPoint), allowSceneObjects = true, value = individualSpawnPoint
        };
        _individualPointField.RegisterValueChangedCallback(evt => individualSpawnPoint = evt.newValue as SpawnPoint);
        parent.Add(_individualPointField);
        _individualTargetField = new ObjectField("초기 타깃 · 선택 사항")
        {
            objectType = typeof(TargetableComponent), allowSceneObjects = true, value = individualInitialTarget
        };
        _individualTargetField.RegisterValueChangedCallback(evt => individualInitialTarget = evt.newValue as TargetableComponent);
        parent.Add(_individualTargetField);
        _individualSpawnButton = new Button(SpawnIndividualEnemy) { text = "지정 지점에 1마리 생성·관찰" };
        parent.Add(_individualSpawnButton);
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
                if (definition == null || string.IsNullOrWhiteSpace(definition.EnemyId)) continue;
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

    private void SpawnIndividualEnemy()
    {
        try
        {
            var definition = _individualDefinitions.Find(item => item != null && item.EnemyId == _individualDefinitionField.value);
            bool success = EnemyTestSceneTools.TrySpawnTestEnemy(binding, definition, individualSpawnPoint, individualInitialTarget,
                out EnemyController enemy, out string message);
            _result.text = message;
            _result.messageType = success ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            if (success)
            {
                Selection.activeGameObject = enemy.gameObject;
                OnSelectionChanged();
            }
        }
        catch (System.Exception error) { ShowError(error); }
        UpdateStatus();
    }
}
