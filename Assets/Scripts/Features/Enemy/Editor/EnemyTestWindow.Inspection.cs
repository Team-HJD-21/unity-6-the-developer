using System;
using System.Text;
using TeamHJD.Game.Enemy.Contracts;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class EnemyTestWindow
{
    [SerializeField] private EnemyDamageTestTarget selectedTestTarget;
    [SerializeField] private EnemyController selectedEnemy;
    [SerializeField] private int testDamage = 10;
    [SerializeField] private AttackSourceType testAttackSource = AttackSourceType.Player;
    private EnemyController _observedEnemy;
    private EnemyHealth _observedHealth;
    private EnemyTargeting _observedTargeting;
    private EnemyAbility _observedAbility;
    private EnemyMovement _observedMovement;
    private EnemyNetworkController _observedNetwork;
    private EnemyDetection _observedDetection;
    private Rigidbody2D _observedBody;
    private ObjectField _testTargetField;
    private ObjectField _enemyField;
    private Label _testTargetStatus;
    private Label _enemyStatus;
    private Button _resetTargetButton;
    private Button _resetAllTargetsButton;
    private Button _damageEnemyButton;
    private Button _lethalDamageButton;

    private void OnEnable() => Selection.selectionChanged += OnSelectionChanged;

    private void OnDisable() => Selection.selectionChanged -= OnSelectionChanged;

    /// <summary>테스트 표적 초기화와 선택한 몬스터 관찰 영역을 구성한다.</summary>
    private void CreateInspection(VisualElement parent)
    {
        parent.Add(new Label("테스트 표적"));
        _testTargetField = new ObjectField("관찰·초기화할 표적")
        {
            objectType = typeof(EnemyDamageTestTarget), allowSceneObjects = true, value = selectedTestTarget
        };
        _testTargetField.RegisterValueChangedCallback(evt =>
        {
            selectedTestTarget = evt.newValue as EnemyDamageTestTarget;
            UpdateInspection();
        });
        parent.Add(_testTargetField);
        _testTargetStatus = new Label();
        _testTargetStatus.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(_testTargetStatus);
        _resetTargetButton = new Button(() => ResetTestTargets(false)) { text = "선택 표적 체력·피격 기록 초기화" };
        _resetAllTargetsButton = new Button(() => ResetTestTargets(true)) { text = "실행기 씬의 모든 테스트 표적 초기화" };
        parent.Add(_resetTargetButton);
        parent.Add(_resetAllTargetsButton);
        parent.Add(new HelpBox("Host의 Play Mode에서 EnemyDamageTestTarget만 초기화합니다. 실제 터렛·CU·플레이어 체력은 변경하지 않습니다.", HelpBoxMessageType.Info));
        parent.Add(new Label("몬스터 관찰 · Hierarchy에서 자식 HitBox를 선택해도 연결됩니다."));
        _enemyField = new ObjectField("관찰 몬스터")
        {
            objectType = typeof(EnemyController), allowSceneObjects = true, value = selectedEnemy
        };
        _enemyField.RegisterValueChangedCallback(evt =>
        {
            selectedEnemy = evt.newValue as EnemyController;
            UpdateInspection();
        });
        parent.Add(_enemyField);
        _enemyStatus = new Label();
        _enemyStatus.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(_enemyStatus);
        var damageField = new IntegerField("테스트 피해량") { value = testDamage };
        damageField.RegisterValueChangedCallback(evt =>
        {
            testDamage = Mathf.Max(1, evt.newValue);
            damageField.SetValueWithoutNotify(testDamage);
        });
        parent.Add(damageField);
        var sourceField = new EnumField("공격자 종류", testAttackSource);
        sourceField.RegisterValueChangedCallback(evt => testAttackSource = (AttackSourceType)evt.newValue);
        parent.Add(sourceField);
        _damageEnemyButton = new Button(() => DamageSelectedEnemy(false)) { text = "선택 몬스터에 피해 적용" };
        _lethalDamageButton = new Button(() => DamageSelectedEnemy(true)) { text = "현재 체력만큼 피해 · 사망 시험" };
        parent.Add(_damageEnemyButton);
        parent.Add(_lethalDamageButton);
        parent.Add(new HelpBox("실제 피격 규칙을 따릅니다. 은신·공격자 종류 등으로 거부될 수 있으며, 사망하면 소환사의 사망 소환도 실행됩니다.", HelpBoxMessageType.Info));
        OnSelectionChanged();
    }

    /// <summary>Hierarchy에서 선택한 Enemy 또는 테스트 표적만 연결한다. 씬 전체를 검색하지 않는다.</summary>
    private void OnSelectionChanged()
    {
        var selected = Selection.activeGameObject;
        if (selected == null) return;
        var enemy = selected.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            selectedEnemy = enemy;
            _enemyField?.SetValueWithoutNotify(enemy);
        }
        var target = selected.GetComponentInParent<EnemyDamageTestTarget>();
        if (target != null)
        {
            selectedTestTarget = target;
            _testTargetField?.SetValueWithoutNotify(target);
        }
        UpdateInspection();
    }

    private bool CanResetTestTargets => EditorApplication.isPlaying && launcher != null &&
        launcher.IsServer && launcher.ExecutionTarget != null;

    private void DamageSelectedEnemy(bool lethal)
    {
        try
        {
            if (launcher == null || selectedEnemy == null)
                throw new InvalidOperationException("Host의 관찰 몬스터를 선택하세요.");
            var health = selectedEnemy.GetComponent<EnemyHealth>();
            int damage = lethal && health != null ? health.CurrentHealth : testDamage;
            bool success = launcher.TryDamageTestEnemy(selectedEnemy, damage, testAttackSource, out string message);
            _result.text = message;
            _result.messageType = success ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
        }
        catch (Exception error) { ShowError(error); }
        UpdateStatus();
    }

    /// <summary>선택 초기화도 전체 초기화도 현재 실행기의 씬과 Host 권한을 확인한다.</summary>
    private void ResetTestTargets(bool resetAll)
    {
        try
        {
            if (!CanResetTestTargets)
                throw new InvalidOperationException("테스트 표적 초기화는 실행기가 준비된 Host의 Play Mode에서만 가능합니다.");
            var scene = launcher.ExecutionTarget.gameObject.scene;
            int count = 0;
            if (resetAll)
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var target in root.GetComponentsInChildren<EnemyDamageTestTarget>(true))
                    {
                        target.ResetHealth();
                        count++;
                    }
            }
            else
            {
                if (selectedTestTarget == null || selectedTestTarget.gameObject.scene != scene)
                    throw new InvalidOperationException("현재 실행기와 같은 씬의 테스트 표적을 선택하세요.");
                selectedTestTarget.ResetHealth();
                count = 1;
            }
            _result.text = $"테스트 표적 {count}개의 체력·피격 기록을 초기화했습니다.";
            _result.messageType = HelpBoxMessageType.Info;
        }
        catch (Exception error) { ShowError(error); }
        UpdateInspection();
    }

    /// <summary>창의 0.5초 상태 갱신에서 실제 게임 상태를 읽는다. 조건 검사나 행동 실행은 호출하지 않는다.</summary>
    private void UpdateInspection()
    {
        if (_testTargetStatus == null || _enemyStatus == null) return;
        bool canDamage = CanResetTestTargets && selectedEnemy != null &&
            selectedEnemy.gameObject.scene == launcher.ExecutionTarget.gameObject.scene &&
            selectedEnemy.TryGetComponent<EnemyNetworkController>(out var network) && network.IsSpawned &&
            network.IsServer && selectedEnemy.TryGetComponent<EnemyHealth>(out var health) && health.CurrentHealth > 0;
        _damageEnemyButton.SetEnabled(canDamage);
        _lethalDamageButton.SetEnabled(canDamage);
        bool sameScene = selectedTestTarget != null && launcher != null && launcher.ExecutionTarget != null &&
            selectedTestTarget.gameObject.scene == launcher.ExecutionTarget.gameObject.scene;
        _resetTargetButton.SetEnabled(CanResetTestTargets && sameScene);
        _resetAllTargetsButton.SetEnabled(CanResetTestTargets);
        _testTargetStatus.text = selectedTestTarget == null ? "Hierarchy에서 테스트 표적을 선택하거나 위 필드에 연결하세요." :
            $"체력: {selectedTestTarget.CurrentHealth}/{selectedTestTarget.MaxHealth}\n피격 {selectedTestTarget.HitCount}회 / 최근 피해 {selectedTestTarget.LastDamage} / 누적 피해 {selectedTestTarget.TotalDamage}";

        if (selectedEnemy == null)
        {
            _observedEnemy = null;
            _enemyStatus.text = "관찰할 몬스터가 없거나 제거되었습니다. Hierarchy에서 몬스터를 선택하세요.";
            return;
        }
        // 관찰 대상이 바뀔 때만 관련 컴포넌트를 가져온다.
        if (_observedEnemy != selectedEnemy) CacheObservedEnemy();
        var text = new StringBuilder();
        text.Append(selectedEnemy.name).Append(" / ").Append(selectedEnemy.GetType().Name)
            .Append("\n활성: ").Append(selectedEnemy.isActiveAndEnabled);
        if (_observedHealth != null)
            text.Append(" / 체력: ").Append(_observedHealth.CurrentHealth).Append('/').Append(_observedHealth.MaxHealth);
        if (_observedNetwork != null)
            text.Append("\nNetwork Spawned: ").Append(_observedNetwork.IsSpawned).Append(" / Server: ").Append(_observedNetwork.IsServer);
        var target = _observedTargeting != null ? _observedTargeting.Target : null;
        text.Append("\n배정 타깃: ").Append(target != null ? target.name : "없음");
        var targetState = _observedTargeting != null ? _observedTargeting.TargetComponent : null;
        if (targetState != null)
            text.Append(" / ").Append(targetState.TargetType).Append("\n타깃 가능: ").Append(targetState.CanBeTargeted)
                .Append(" / 배정 인원: ").Append(targetState.AssignedAttackerCount).Append('/').Append(targetState.MaxAttackers);
        text.Append("\n분대 대표: ").Append(selectedEnemy.IsSquadRepresentative)
            .Append(" / 공격 인원 집계 포함: ").Append(selectedEnemy.IsIncludedInAttackerLimit);
        if (_observedMovement != null)
            text.Append("\n남은 경로: ").Append(_observedMovement.HasPath).Append(" / 설정 이동속도: ").Append(_observedMovement.MoveSpeed.ToString("0.##"));
        if (_observedBody != null)
            text.Append(" / 실제 속력: ").Append(_observedBody.linearVelocity.magnitude.ToString("0.##"));
        if (_observedAbility == null) text.Append("\n연결된 능력 없음");
        else
        {
            text.Append("\n능력: ").Append(_observedAbility.GetType().Name)
                .Append("\n능력 활성: ").Append(_observedAbility.isActiveAndEnabled)
                .Append(" / 실행 중: ").Append(_observedAbility.IsExecuting);
            var facing = _observedAbility.FacingTarget;
            text.Append("\n능력이 바라보는 대상: ").Append(facing != null ? facing.name : "별도 대상 없음 · 배정 타깃 사용");
        }
        if (_observedDetection != null) text.Append("\n탐지됨: ").Append(_observedDetection.IsDetected);
        _enemyStatus.text = text.ToString();
    }

    private void CacheObservedEnemy()
    {
        _observedEnemy = selectedEnemy;
        _observedHealth = selectedEnemy.GetComponent<EnemyHealth>();
        _observedTargeting = selectedEnemy.GetComponent<EnemyTargeting>();
        _observedMovement = selectedEnemy.GetComponent<EnemyMovement>();
        _observedNetwork = selectedEnemy.GetComponent<EnemyNetworkController>();
        _observedDetection = selectedEnemy.GetComponent<EnemyDetection>();
        _observedBody = selectedEnemy.GetComponent<Rigidbody2D>();
        // 여러 능력 컴포넌트가 있어도 Controller가 실제 사용하는 참조를 읽는다.
        var controller = new SerializedObject(selectedEnemy);
        _observedAbility = controller.FindProperty("enemyAbility")?.objectReferenceValue as EnemyAbility;
    }
}
