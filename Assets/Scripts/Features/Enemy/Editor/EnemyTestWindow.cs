using System;
using System.Collections.Generic;
using System.Text;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Infrastructure;
using Unity.Netcode;
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
    private Button _hostButton;
    private Button _clientButton;
    private Button _fixedButton;
    private Button _planButton;
    private Button _clearButton;
    private NgoNetworkSession _testSession;

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
        scroll.Add(new Label("Core Match → EncounterRuntime → Planner → Executor"));
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
        _hostButton = new Button(() => StartNetwork(true)) { text = "Start Host" };
        scroll.Add(_hostButton);
        _clientButton = new Button(() => StartNetwork(false)) { text = "Start Client" };
        scroll.Add(_clientButton);
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
        _result = new HelpBox("Play → Core Host/Client 시작 → 생성 요청 순서로 실행하세요.", HelpBoxMessageType.Info);
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
            if (candidates.Length == 1) binding = candidates[0];
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
        bool ready = EditorApplication.isPlaying && binding != null && binding.IsServer && !binding.BoundMatchId.IsEmpty;
        _hostButton.SetEnabled(EditorApplication.isPlaying && binding != null && !binding.IsListening);
        _clientButton.SetEnabled(EditorApplication.isPlaying && binding != null && !binding.IsListening);
        _fixedButton.SetEnabled(ready && SelectedPreset() != null);
        _planButton.SetEnabled(ready);
        _clearButton.SetEnabled(ready && binding.ExecutionTarget != null);
        _individualSpawnButton?.SetEnabled(ready && binding.ExecutionTarget != null && binding.ExecutionTarget.CanSpawn &&
            individualSpawnPoint != null && _individualDefinitions.Count > 0);
        _status.text = binding == null ? "현재 씬의 EncounterSceneBinding을 지정하세요." :
            $"Match: {binding.BoundMatchId}\nHost: {binding.IsServer} / 실행기: {(binding.ExecutionTarget != null ? binding.ExecutionTarget.name : "미연결")}\n요청 생성 생존 적: {binding.ActiveEnemyCount}마리 · 별도 소환물 제외";
        UpdateInspection();
    }

    private void StartNetwork(bool host)
    {
        try
        {
            if (binding == null) throw new InvalidOperationException("EncounterSceneBinding을 먼저 연결하세요.");

            var coreBindings = UnityEngine.Object.FindObjectsByType<InitialStageNetworkBinding>(FindObjectsSortMode.None);
            if (coreBindings.Length == 1)
            {
                bool started = host ? coreBindings[0].TryStartHost() : coreBindings[0].TryStartClient();
                _result.text = started ? (host ? "Core Host 연결을 시작했습니다." : "Core Client 연결을 시작했습니다.") : "Core가 연결 시작을 거부했습니다. 현재 연결 상태를 확인하세요.";
                _result.messageType = started ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            }
            else if (coreBindings.Length > 1)
            {
                throw new InvalidOperationException("현재 씬에 InitialStageNetworkBinding이 여러 개 있습니다.");
            }
            else
            {
                NetworkManager manager = binding.GetComponent<NetworkManager>();
                if (manager == null) throw new InvalidOperationException("EncounterSceneBinding에 NetworkManager가 없습니다.");
                if (manager.IsListening) throw new InvalidOperationException("현재 NetworkManager는 이미 연결되어 있습니다.");
                _testSession ??= new NgoNetworkSession(manager);
                bool started = host ? _testSession.TryStartHost() : _testSession.TryStartClient();
                _result.text = started ? (host ? "테스트 씬의 Core NgoNetworkSession으로 Host를 시작했습니다." : "테스트 씬의 Core NgoNetworkSession으로 Client를 시작했습니다.") : "Core NgoNetworkSession이 연결 시작을 거부했습니다.";
                _result.messageType = started ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            }
        }
        catch (Exception error) { ShowError(error); }
        UpdateStatus();
    }

    /// <summary>직접 생성하지 않고 기존 런처의 실제 Runtime 경로에 요청한다.</summary>
    private void RunRequest(bool selectBySituation)
    {
        try
        {
            var previous = binding.RecentDiagnostics;
            long lastSequence = previous.Count > 0 ? previous[previous.Count - 1].Sequence : 0;
            string message;
            bool success = selectBySituation
                ? binding.RequestSpawn(maximumEnemyCount, out message)
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
            bool success = binding.TryClearTestSceneEnemies(out string message);
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
