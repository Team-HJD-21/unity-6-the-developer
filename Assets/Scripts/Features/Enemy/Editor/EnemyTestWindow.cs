using System;
using System.Collections.Generic;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Bootstrap.SceneComposition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Enemy 테스트 씬에서 실제 Encounter 요청과 생성 결과를 반복 확인하는 창이다.</summary>
public sealed partial class EnemyTestWindow : EditorWindow
{
    [SerializeField] private EncounterSceneBinding binding;
    [SerializeField] private int maximumEnemyCount = 6;
    private readonly List<EnemySquadPreset> _presets = new();
    private ObjectField _bindingField;
    private DropdownField _presetField;
    private Label _status;
    private Label _composition;
    private HelpBox _result;
    private Button _fixedButton;
    private Button _planButton;
    private Button _clearButton;
    private bool _wasReady;

    [MenuItem("Tools/TeamHJD/The Developer/Enemy Test")]
    public static void Open()
    {
        var window = GetWindow<EnemyTestWindow>("Enemy Test");
        window.minSize = new Vector2(420, 440);
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        var scroll = new ScrollView();
        rootVisualElement.Add(scroll);
        scroll.Add(new Label("Core Match → EncounterPipeline → Planner → Executor"));
        _bindingField = new ObjectField("Encounter 연결")
        {
            objectType = typeof(EncounterSceneBinding), allowSceneObjects = true, value = binding
        };
        _bindingField.RegisterValueChangedCallback(evt =>
        {
            binding = evt.newValue as EncounterSceneBinding;
            UpdateStatus();
            ShowComposition();
        });
        scroll.Add(_bindingField);
        scroll.Add(new Button(RefreshReferences) { text = "현재 씬 연결·카탈로그 새로고침" });
        _status = new Label();
        _status.style.whiteSpace = WhiteSpace.Normal;
        scroll.Add(_status);
        var countField = new IntegerField("요청당 생성 상한") { value = maximumEnemyCount };
        countField.RegisterValueChangedCallback(evt =>
        {
            maximumEnemyCount = Mathf.Max(1, evt.newValue);
            countField.SetValueWithoutNotify(maximumEnemyCount);
        });
        scroll.Add(countField);
        _presetField = new DropdownField("지정 편성");
        _presetField.RegisterValueChangedCallback(_ => ShowComposition());
        scroll.Add(_presetField);
        _composition = new Label();
        _composition.style.whiteSpace = WhiteSpace.Normal;
        scroll.Add(_composition);
        _fixedButton = new Button(() => RunRequest(false)) { text = "선택한 편성 요청" };
        _planButton = new Button(() => RunRequest(true)) { text = "현재 상황으로 편성 선택·요청" };
        scroll.Add(_fixedButton);
        scroll.Add(_planButton);
        scroll.Add(new HelpBox("상한은 정확한 생성 수가 아닙니다. 점유율 예산·완전한 편성·출현 셀 수용량을 적용합니다.", HelpBoxMessageType.Info));
        _clearButton = new Button(ClearEnemies) { text = "시험 씬 몬스터 전체 정리 · 소환물 포함" };
        scroll.Add(_clearButton);
        scroll.Add(new HelpBox("같은 씬·같은 서버의 네트워크 몬스터를 모두 제거합니다. 사망 효과는 실행하지 않습니다. 다른 도구의 반복 요청은 별도로 중지하세요.", HelpBoxMessageType.Info));
        _result = new HelpBox("EnemyTest에서 Play하면 Core가 네트워크와 Match를 시작합니다. 준비 후 생성 요청을 실행하세요.", HelpBoxMessageType.Info);
        scroll.Add(_result);
        CreateSingleSpawn(scroll);
        CreateInspection(scroll);
        RefreshReferences();
        // 창이 열린 동안에만 상태를 갱신한다. 프레임마다 씬을 검색하지 않는다.
        rootVisualElement.schedule.Execute(UpdateStatus).Every(500);
    }

    /// <summary>사용자가 요청할 때만 씬의 Encounter 연결과 등록 편성을 다시 찾는다.</summary>
    private void RefreshReferences()
    {
        if (binding == null)
        {
            var candidates = UnityEngine.Object.FindObjectsByType<EncounterSceneBinding>(FindObjectsSortMode.None);
            var testBindings = Array.FindAll(candidates, item => item.gameObject.scene.path == EnemyTestSceneTools.ScenePath || EnemyTestSceneTools.IsTestScene(item));
            if (testBindings.Length == 1) binding = testBindings[0];
        }
        _bindingField.SetValueWithoutNotify(binding);
        string selected = _presetField.value;
        _presets.Clear();
        var choices = new List<string>();
        var catalog = EnemySquadPresetCatalog.Load();
        if (catalog != null)
        {
            foreach (var preset in catalog.Presets)
            {
                if (preset == null || string.IsNullOrWhiteSpace(preset.SquadOrder)) continue;
                _presets.Add(preset);
                choices.Add(preset.SquadOrder);
            }
        }
        _presetField.choices = choices;
        _presetField.SetValueWithoutNotify(choices.Contains(selected) ? selected : choices.Count > 0 ? choices[0] : "");
        RefreshSingleSpawnOptions();
        ShowComposition();
        UpdateStatus();
    }

    private void ShowComposition()
    {
        if (_composition == null) return;
        var preset = SelectedPreset();
        if (preset == null) { _composition.text = "등록된 편성이 없습니다."; return; }
        var text = new StringBuilder();
        foreach (var entry in preset.Composition)
            if (entry != null) text.Append(entry.EnemyId).Append(" × ").Append(entry.Weight).Append("  ");
        text.Append("\n한 편성 ").Append(preset.TotalWeight).Append("마리");
        if (binding != null && binding.ExecutionTarget != null)
            text.Append(" / 비용 ").Append(preset.CalculateCost(binding.ExecutionTarget.Catalog));
        _composition.text = text.ToString();
    }

    private EnemySquadPreset SelectedPreset()
        => _presets.Find(preset => preset != null && preset.SquadOrder == _presetField.value);

    private void UpdateStatus()
    {
        if (_status == null) return;
        bool ready = EditorApplication.isPlaying && EnemyTestSceneTools.IsTestScene(binding) && binding.IsServer && !binding.BoundMatchId.IsEmpty;
        // Core가 Match를 연결한 뒤 카탈로그와 시험 구역을 한 번 갱신한다.
        bool becameReady = ready && !_wasReady;
        _wasReady = ready;
        if (becameReady) RefreshReferences();
        _fixedButton.SetEnabled(ready && SelectedPreset() != null);
        _planButton.SetEnabled(ready);
        _clearButton.SetEnabled(ready && binding.ExecutionTarget != null);
        _individualSpawnButton?.SetEnabled(ready && binding.ExecutionTarget != null && binding.ExecutionTarget.CanSpawn &&
            individualSpawnPoint != null && _individualDefinitions.Count > 0);
        _status.text = binding == null ? "현재 씬의 EncounterSceneBinding을 지정하세요." :
            $"Match: {binding.BoundMatchId}\nHost: {binding.IsServer} / 실행기: {(binding.ExecutionTarget != null ? binding.ExecutionTarget.name : "미연결")}\n요청 생성 생존 적: {binding.ActiveEnemyCount}마리 · 별도 소환물 제외";
        UpdateInspection();
    }

    /// <summary>Core의 현재 Match 요청 API를 사용해 시험 편성을 요청한다.</summary>
    private void RunRequest(bool selectBySituation)
    {
        try
        {
            if (!EnemyTestSceneTools.IsTestScene(binding)) throw new InvalidOperationException("EnemyTest 씬에서만 요청할 수 있습니다.");
            var previous = binding.RecentDiagnostics;
            long lastSequence = previous.Count > 0 ? previous[previous.Count - 1].Sequence : 0;
            string message;
            bool success = selectBySituation
                ? EnemyTestSceneTools.RequestSpawn(binding, maximumEnemyCount, out message)
                : binding.TryRequestCurrentEncounter(SelectedPreset().SquadOrder, maximumEnemyCount,
                    EncounterRequestSource.DebugWindow, out message);
            var diagnostics = binding.RecentDiagnostics;
            for (int i = diagnostics.Count - 1; i >= 0; i--)
            {
                var item = diagnostics[i];
                if (item.Sequence <= lastSequence) break;
                if (item.Kind != GameDiagnosticKind.RequestCompleted && item.Kind != GameDiagnosticKind.RequestFailed) continue;
                message += item.IsExecutionCountUnknown ? "\n실행 예외로 생성 수 확인 필요" :
                    $"\n계획 {item.PlannedCount} / 실제 생성 {item.ActualCount}";
                break;
            }
            _result.text = message;
            _result.messageType = success ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
        }
        catch (Exception error) { ShowError(error); }
        UpdateStatus();
    }

    private void ClearEnemies()
    {
        try
        {
            bool success = EnemyTestSceneTools.TryClearTestSceneEnemies(binding, out string message);
            _result.text = message;
            _result.messageType = success ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
        }
        catch (Exception error) { ShowError(error); }
        UpdateStatus();
    }

    private void ShowError(Exception error)
    {
        _result.text = error.Message;
        _result.messageType = HelpBoxMessageType.Error;
        Debug.LogException(error);
    }
}
