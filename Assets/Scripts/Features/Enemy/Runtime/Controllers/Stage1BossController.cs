using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>페이즈별 행동 순서와 탈진 시간을 관리한다. 효과 실행은 각 능력에 맡긴다.</summary>
public sealed class Stage1BossController : BossEnemyController
{
    private enum PatternStep
    {
        Attack, DoubleAttack, TripleAttack, Stomp, DoubleStomp, Roar, Recovery, Rest,
        Charge, DoubleCharge, StompBarrage
    }

    // Inspector 설정과 제작 참조
    [Header("능력")]
    [SerializeField] private BossCloseRangeAbility normalAttack;
    [SerializeField] private EnemySummonAbility summonAbility;
    [SerializeField] private BossStompAbility stompAbility;
    [SerializeField] private BossChargeAbility chargeAbility;

    [Header("참조")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private BossDamageRule damageRule;
    [SerializeField] private BossSkillNetworkView skillView;

    [Header("페이즈 체력 비율")]
    [SerializeField, Range(0f, 1f)] private float phase2HealthRatio = 0.7f;
    [SerializeField, Range(0f, 1f)] private float phase3HealthRatio = 0.35f;

    [Header("페이즈별 포효 소환")]
    [SerializeField, Min(1)] private int phase2SummonCount = 2;
    [SerializeField, Min(1)] private int phase3SummonCount = 4;

    [Header("페이즈별 원 예고 시간")]
    [SerializeField, Min(0.1f)] private float phase1WarningDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float phase2WarningDuration = 1f;
    [SerializeField, Min(0.1f)] private float phase3WarningDuration = .8f;

    [Header("3페이즈 연속 원")]
    [SerializeField, Min(1)] private int phase3StompCount = 8;

    [Header("페이즈별 행동 순서")]
    [Tooltip("Attack은 사거리까지 접근 후 공격한다. Recovery는 짧은 후딜, 마지막 Rest는 사이클 종료 대기다.")]
    [SerializeField] private PatternStep[] phase1Pattern =
    {
        PatternStep.Charge, PatternStep.DoubleAttack, PatternStep.Recovery, PatternStep.Stomp,
        PatternStep.Charge, PatternStep.Attack, PatternStep.Rest
    };
    [SerializeField] private PatternStep[] phase2Pattern =
    {
        PatternStep.Charge, PatternStep.DoubleAttack, PatternStep.Recovery, PatternStep.DoubleStomp,
        PatternStep.Attack, PatternStep.Recovery, PatternStep.Roar,
        PatternStep.Charge, PatternStep.DoubleAttack, PatternStep.DoubleStomp, PatternStep.Rest
    };
    [SerializeField] private PatternStep[] phase3Pattern =
    {
        PatternStep.DoubleCharge, PatternStep.TripleAttack, PatternStep.Recovery, PatternStep.StompBarrage,
        PatternStep.Roar, PatternStep.DoubleCharge, PatternStep.DoubleAttack,
        PatternStep.StompBarrage, PatternStep.Rest
    };

    [Header("행동 묶음 사이 짧은 후딜")]
    [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;
    [SerializeField, Min(0f)] private float phase3RecoveryDuration = 0.4f;

    [Header("근접 공격 대기 상한")]
    [Tooltip("각 근접 공격을 시작하지 못한 채 기다릴 수 있는 시간이다. 실행 중인 모션은 끊지 않는다.")]
    [SerializeField, Min(0.1f)] private float attackWaitTimeout = 3f;

    [Header("큰 사이클 종료 후 대기")]
    [SerializeField, Min(0f)] private float phase1RestDuration = 2f;
    [SerializeField, Min(0f)] private float phase2RestDuration = 2f;
    [SerializeField, Min(0f)] private float phase3RestDuration = 3f;

    // 실행 중 참조와 상태
    private PatternStep[] _pattern;
    private int _patternIndex;
    private int _phase = 1;
    private int _remainingAttacks;
    private double _waitEndsAt;
    private double _attackWaitEndsAt;
    private bool _cycleStarted;
    private bool _configured;

    public int Phase => _phase;
    public bool IsResting => _cycleStarted &&
        (CurrentStep == PatternStep.Recovery || CurrentStep == PatternStep.Rest);
    private PatternStep CurrentStep => _pattern[_patternIndex];

    // Unity 생명주기
    protected override void Awake()
    {
        if (normalAttack == null) normalAttack = GetComponent<BossCloseRangeAbility>();
        if (summonAbility == null) summonAbility = GetComponent<EnemySummonAbility>();
        if (stompAbility == null) stompAbility = GetComponent<BossStompAbility>();
        if (chargeAbility == null) chargeAbility = GetComponent<BossChargeAbility>();
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (damageRule == null) damageRule = GetComponent<BossDamageRule>();
        if (skillView == null) skillView = GetComponent<BossSkillNetworkView>();
        enemyAbility = normalAttack;
        base.Awake();
        _configured = normalAttack != null && summonAbility != null && stompAbility != null && chargeAbility != null &&
                      enemyHealth != null && damageRule != null && skillView != null &&
                      IsValidPattern(phase1Pattern) && IsValidPattern(phase2Pattern) && IsValidPattern(phase3Pattern);
        if (!_configured)
            Debug.LogError("Stage1Boss의 참조와 행동 순서를 확인하세요. 각 순서는 능력을 포함하고 Rest로 끝나야 합니다.", this);
    }

    protected override void OnDisable()
    {
        if (damageRule != null) damageRule.SetExposed(false);
        _cycleStarted = false;
        _phase = 1;
        base.OnDisable();
    }

    private void OnValidate() => phase3HealthRatio = Mathf.Min(phase3HealthRatio, phase2HealthRatio);

    // 외부 호출과 상태 변경
    public override void ExecuteBehavior()
    {
        if (!_configured) { StopMoving(); return; }
        if (!_cycleStarted) BeginCycle();

        if (IsResting)
        {
            StopMoving();
            if (FieldSimulationTime.Seconds(this) >= _waitEndsAt)
                AdvanceStep();
            return;
        }
        base.ExecuteBehavior();
    }

    public override void OnDeath()
    {
        if (damageRule != null) damageRule.SetExposed(false);
        base.OnDeath();
    }

    // 파생 클래스 공통 처리
    protected override EnemyAbility SelectAbility()
    {
        // 공통 컨트롤러가 이전 능력의 완료를 처리한 뒤에만 새 페이즈를 선택한다.
        // 완료 알림이 새 포효 단계를 건너뛰는 것을 막는다.
        if (GetHealthPhase() > _phase)
            BeginCycle();

        // 타깃을 계속 쫓기만 해도 원 공격·포효까지 진행할 수 있게 한다.
        if (IsAttackStep(CurrentStep) && FieldSimulationTime.Seconds(this) >= _attackWaitEndsAt)
            AdvanceStep();
        if ((CurrentStep == PatternStep.Charge || CurrentStep == PatternStep.DoubleCharge) && chargeAbility.IsInStoppingRange)
            AdvanceStep();

        return CurrentStep switch
        {
            PatternStep.Roar => summonAbility,
            PatternStep.Stomp or PatternStep.DoubleStomp or PatternStep.StompBarrage => stompAbility,
            PatternStep.Charge or PatternStep.DoubleCharge => chargeAbility,
            PatternStep.Attack or PatternStep.DoubleAttack or PatternStep.TripleAttack => normalAttack,
            _ => null
        };
    }

    protected override void PlaySelectedAbilityAnimation(EnemyAbility ability)
    {
        // 돌진은 능력이 준비·이동 모션을 직접 처리한다.
        if (ability == chargeAbility) return;
        if (ability == summonAbility) skillView.PlaySkill(BossSkill.Roar);
        else if (ability == stompAbility) skillView.PlaySkill(BossSkill.Stomp);
        else base.PlaySelectedAbilityAnimation(ability);
    }

    /// <summary>실제 능력이 끝난 후에만 다음 단계로 진행한다.</summary>
    protected override void OnAbilityCompleted(EnemyAbility ability)
    {
        if (ability == normalAttack && --_remainingAttacks > 0)
        {
            _attackWaitEndsAt = FieldSimulationTime.Seconds(this) + attackWaitTimeout;
            return;
        }
        AdvanceStep();
    }

    // 내부 처리
    private void BeginCycle()
    {
        damageRule.SetExposed(false);
        int nextPhase = GetHealthPhase();
        bool phaseChanged = nextPhase > _phase;
        // 회복해도 이전 페이즈로 돌아가지 않는다.
        _phase = Mathf.Max(_phase, nextPhase);
        _pattern = _phase == 3 ? phase3Pattern : _phase == 2 ? phase2Pattern : phase1Pattern;
        // 새 페이즈에 들어갈 때는 먼저 포효하고 이후 큰 사이클의 나머지 행동으로 진행한다.
        int roarIndex = phaseChanged && _phase >= 2 ? Array.IndexOf(_pattern, PatternStep.Roar) : -1;
        _patternIndex = roarIndex >= 0 ? roarIndex : 0;
        _cycleStarted = true;
        EnterStep();
    }

    /// <summary>한 행동 묶음이 끝나면 다음으로 넘어가며, 긴 후딜 종료 후 새 사이클을 시작한다.</summary>
    private void AdvanceStep()
    {
        if (++_patternIndex >= _pattern.Length) BeginCycle();
        else EnterStep();
    }

    /// <summary>연속 공격 수·원 파동 수·소환 수·후딜을 이번 행동에 맞게 설정한다.</summary>
    private void EnterStep()
    {
        PatternStep step = CurrentStep;
        _remainingAttacks = step == PatternStep.TripleAttack ? 3 : step == PatternStep.DoubleAttack ? 2 : 1;
        if (IsAttackStep(step))
            _attackWaitEndsAt = FieldSimulationTime.Seconds(this) + attackWaitTimeout;
        damageRule.SetExposed(step == PatternStep.Rest && _phase >= 2);

        if (step == PatternStep.Roar)
            summonAbility.SetSummonCount(_phase == 3 ? phase3SummonCount : phase2SummonCount);
        else if (step == PatternStep.Charge || step == PatternStep.DoubleCharge)
            chargeAbility.SetChargeCount(step == PatternStep.DoubleCharge ? 2 : 1);
        else if (step == PatternStep.Stomp || step == PatternStep.DoubleStomp || step == PatternStep.StompBarrage)
        {
            stompAbility.SetWaveCount(step == PatternStep.StompBarrage ? phase3StompCount : step == PatternStep.DoubleStomp ? 2 : 1);
            stompAbility.SetWarningDuration(_phase == 3 ? phase3WarningDuration : _phase == 2 ? phase2WarningDuration : phase1WarningDuration);
        }

        if (!IsResting) return;
        float duration = step == PatternStep.Recovery
            ? (_phase == 3 ? phase3RecoveryDuration : recoveryDuration)
            : (_phase == 3 ? phase3RestDuration : _phase == 2 ? phase2RestDuration : phase1RestDuration);
        _waitEndsAt = FieldSimulationTime.Seconds(this) + duration;
    }

    private int GetHealthPhase()
    {
        float ratio = (float)enemyHealth.CurrentHealth / Mathf.Max(1, enemyHealth.MaxHealth);
        return ratio <= phase3HealthRatio ? 3 : ratio <= phase2HealthRatio ? 2 : 1;
    }

    private static bool IsAttackStep(PatternStep step) =>
        step == PatternStep.Attack || step == PatternStep.DoubleAttack || step == PatternStep.TripleAttack;

    /// <summary>빈 순서나 후딜만 있는 사이클을 실행하지 않도록 제작 순서를 검사한다.</summary>
    private static bool IsValidPattern(PatternStep[] pattern)
    {
        if (pattern == null || pattern.Length == 0 || pattern[pattern.Length - 1] != PatternStep.Rest)
            return false;
        bool hasAbility = false;
        for (int index = 0; index < pattern.Length; index++)
        {
            PatternStep step = pattern[index];
            if (!Enum.IsDefined(typeof(PatternStep), step)) return false;
            if (step == PatternStep.Rest && index != pattern.Length - 1) return false;
            hasAbility |= step != PatternStep.Recovery && step != PatternStep.Rest;
        }
        return hasAbility;
    }
}
