// Scene의 터렛/CU를 Match 소유 지역 Grid에 연결합니다.
// 정적 정책은 구역을 적용하고 동적 정책은 잠금 없이 활성 터렛의 점령만 갱신합니다.
// 이 컴포넌트는 지역 상태를 별도 소유하지 않고 Match 종료 전에 구독과 정책을 해제합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    [DisallowMultipleComponent]
    public sealed partial class RegionSceneBinding : MonoBehaviour
    {
        [SerializeField] private FieldTurretController _controller;
        [SerializeField] private Transform _controlUnit;
        [SerializeField] private RegionPolicyMode _initialMode = RegionPolicyMode.Static;
        [Tooltip("점유율 분모에서 제외할 월드 XY 사각 영역. Field 사건 진입로 등이며 적용에는 명시적 Grid 재설정이 필요합니다.")]
        [SerializeField] private Bounds[] _captureExcludedAreas = Array.Empty<Bounds>();
        private MatchSession _session;
        private IRegionGridSource _source;
        private ITurretEntityIdResolver _entityIds;
        private RegionGridDefinition _definition;
        private readonly DynamicRegionCapturePolicy _dynamicPolicy = new DynamicRegionCapturePolicy();
        private IReadOnlyList<RegionTurretInput> _lastInputs;
        private BattlefieldPoint? _lastControlUnitPosition;
        private int[] _dynamicInitialCells;
        private Func<bool> _mayWrite;
        private float _nextSample;
        private bool _dirty;
        private int _zoneCount;
        public IRegionGridSource Source => _session == null || !_session.IsDisposed ? _source : null;
        public RegionCaptureEvaluation LastEvaluation { get; private set; }
        public string LastDynamicError { get; private set; }
        public bool HasWriteAuthority => _session != null && !_session.IsDisposed &&
            (_session.State.Phase == MatchPhase.Preparing || _session.State.Phase == MatchPhase.Running) &&
            _mayWrite != null && _mayWrite();

        public RegionPolicyMode InitialMode => _initialMode;
        public FieldTurretController Controller => _controller;
        public Transform ControlUnit => _controlUnit;
        public RegionGridDefinition CaptureInitialDefinition() => CreateDefinition(_initialMode, out _);

        public void BindMatch(MatchSession session, Func<bool> mayWrite = null, ITurretEntityIdResolver entityIds = null)
            => BindMatch(session, CreateDefinition(_initialMode, out _), _initialMode, mayWrite, entityIds);

        /// <summary>네트워크 시작 때는 Inspector가 아니라 Host가 준비한 불변 정의를 적용합니다.</summary>
        public void BindMatch(MatchSession session, RegionGridDefinition definition,
            RegionPolicyMode mode, Func<bool> mayWrite = null, ITurretEntityIdResolver entityIds = null)
        {
            if (_source != null) throw new InvalidOperationException("Region scene is already bound.");
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (_controller == null)
                throw new InvalidOperationException("Assign FieldTurretController to RegionSceneBinding.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            int zoneCount = 0;
            foreach (int regionId in definition.RegionIds) zoneCount = Math.Max(zoneCount, regionId);
            _source = session.InitializeRegions(definition, mode);
            _session = session;
            _definition = definition;
            _zoneCount = zoneCount;
            _mayWrite = mayWrite ?? (() => true);
            _entityIds = entityIds;
            _session.Disposing += UnbindMatch;
            try
            {
                if (HasWriteAuthority)
                {
                    if (mode == RegionPolicyMode.Static) InitializeStaticCapture();
                    else RefreshDynamic(true);
                }
                _controller.SetRegionAccessPolicy(new RegionAccessPolicy(_source));
                FieldTurretInstanceRegistry.SnapshotChanged += OnTurretChanged;
                _dirty = true;
            }
            catch { UnbindMatch(); throw; }
        }

        public void UnbindMatch()
        {
            if (_source == null && _session == null) return;
            if (_session != null) _session.Disposing -= UnbindMatch;
            FieldTurretInstanceRegistry.SnapshotChanged -= OnTurretChanged;
            ReleaseCombatSources();
            _staticCapture.Clear();
            _session = null;
            _source = null;
            _lastInputs = null;
            _lastControlUnitPosition = null;
            _dynamicInitialCells = null;
            LastDynamicError = null;
            _mayWrite = null;
            _entityIds = null;
            LastEvaluation = null;
            if (_controller != null) _controller.SetRegionAccessPolicy(null);
        }

        /// <summary>Client는 Host의 확정 상태를 연결하며 Match/정책/삼각형 계산을 새로 만들지 않습니다.</summary>
        public void BindReplica(IRegionGridSource source)
        {
            if (_source != null) throw new InvalidOperationException("Region scene is already bound.");
            if (source == null || source.Snapshot == null) throw new ArgumentException("A ready region replica is required.", nameof(source));
            if (_controller == null) throw new InvalidOperationException("Assign FieldTurretController to RegionSceneBinding.");
            _source = source;
            try { _controller.SetRegionAccessPolicy(new RegionAccessPolicy(source)); }
            catch { _source = null; throw; }
        }

        public bool ResetPolicy(RegionPolicyMode mode, bool resetConfirmed)
        {
            if (!resetConfirmed || !HasWriteAuthority) return false;
            RegionGridDefinition definition = CreateDefinition(mode, out int zoneCount);
            RegionCaptureEvaluation evaluation = null;
            IReadOnlyList<int> cells;
            if (mode == RegionPolicyMode.Dynamic)
            {
                evaluation = EvaluateDynamic(definition, null);
                cells = evaluation.CellIndices;
            }
            else if (mode == RegionPolicyMode.Static) cells = InitialControlUnitCells(definition);
            else throw new ArgumentOutOfRangeException(nameof(mode));
            // 새 정의와 초기 결과를 한 번에 발행합니다. 빈 정적 중간 상태로 터렛을 모두 끄지 않습니다.
            _dynamicInitialCells = null;
            _session.ResetRegions(definition, mode, cells, mode == RegionPolicyMode.Static ? new[] { 1 } : null);
            ResetStaticCapture(definition);
            _definition = definition;
            _zoneCount = zoneCount;
            LastEvaluation = evaluation;
            _lastInputs = null;
            _lastControlUnitPosition = null;
            LastDynamicError = null;
            _dirty = true;
            return true;
        }

        public bool TryUnlockThroughZone(int zone)
        {
            if (!HasWriteAuthority || _source.Snapshot.Mode != RegionPolicyMode.Static || zone < 1 || zone > _zoneCount)
                return false;
            _session.UnlockRegionsThrough(_source.Snapshot, zone);
            return true;
        }

        /// <summary>체크포인트 실행기가 사전 검증한 Grid 정의·모드·점유 셀을 현재 Host에 적용합니다.</summary>
        /// <remarks>정적 첫 구역을 다시 해금하지 않습니다. 동적 점유는 실제 터렛 복원이 끝난 뒤 다시 평가합니다.</remarks>
        public void RestoreCheckpoint(RegionGridDefinition definition, RegionPolicyMode mode, int[] capturedCells, int[] unlockedRegions)
        {
            if (!HasWriteAuthority) throw new InvalidOperationException("Only the current authority can restore regions.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (mode == RegionPolicyMode.Static)
            {
                var cells = new HashSet<int>(capturedCells);
                foreach (int cell in InitialControlUnitCells(definition)) cells.Add(cell);
                capturedCells = new List<int>(cells).ToArray();
            }
            _session.ResetRegions(definition, mode, capturedCells, unlockedRegions);
            ResetStaticCapture(definition);
            _definition = definition;
            _zoneCount = 0;
            foreach (int id in definition.RegionIds) _zoneCount = Math.Max(_zoneCount, id);
            LastEvaluation = null;
            _lastInputs = null;
            _lastControlUnitPosition = null;
            _dirty = true;
        }

        private RegionGridDefinition CreateDefinition(RegionPolicyMode mode, out int zoneCount)
        {
            if (_controller == null) throw new InvalidOperationException("Assign FieldTurretController to RegionSceneBinding.");
            zoneCount = 0;
            if (mode == RegionPolicyMode.Dynamic)
                return WithWalkableMask(FieldTurretRegionGridAdapter.CreateDefinition(_controller.CreateUnlockGridConfiguration()));
            if (_controlUnit == null) throw new InvalidOperationException("Static regions require a CU Transform.");
            FieldTurretUnlockZones zones = _controller.CreateUnlockZoneDefinition(_controlUnit.position);
            zoneCount = zones.ZoneCount;
            return WithWalkableMask(FieldTurretRegionGridAdapter.CreateDefinition(zones));
        }

        private void OnTurretChanged(int instanceId) => _dirty = true;

        private void Update()
        {
            if (!HasWriteAuthority || _source.Snapshot.Mode != RegionPolicyMode.Dynamic ||
                _session.State.Phase != MatchPhase.Running) return;
            if (!_dirty && Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + 0.1f;
            try { RefreshDynamic(_dirty); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                if (LastDynamicError != error.Message)
                    Debug.LogError("동적 지역 설정을 확인하세요: " + error.Message, this);
                LastDynamicError = error.Message;
                _dirty = false;
            }
        }

        private void RefreshDynamic(bool force)
        {
            IReadOnlyList<RegionTurretInput> inputs = FieldTurretRegionGridAdapter.CaptureTurrets(_controller, _entityIds);
            BattlefieldPoint controlUnit = ControlUnitPosition();
            _dirty = false;
            if (!force && SameInputs(inputs, _lastInputs) && _lastControlUnitPosition == controlUnit)
            {
                LastDynamicError = null;
                return;
            }
            RegionGridSnapshot basis = _source.Snapshot;
            RegionCaptureEvaluation evaluation = _dynamicPolicy.EvaluateConnected(_definition, inputs,
                controlUnit, _dynamicInitialCells);
            RegionUpdateStatus result = _session.UpdateRegionCapture(basis, evaluation.CellIndices, RegionCaptureUpdateMode.Replace);
            if (result != RegionUpdateStatus.Applied && result != RegionUpdateStatus.Unchanged)
                throw new InvalidOperationException("Region input no longer belongs to the current grid revision.");
            _lastInputs = inputs;
            _lastControlUnitPosition = controlUnit;
            LastEvaluation = evaluation;
            LastDynamicError = null;
        }

        private static bool SameInputs(IReadOnlyList<RegionTurretInput> left, IReadOnlyList<RegionTurretInput> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
                if (left[index].EntityId != right[index].EntityId || left[index].Position != right[index].Position ||
                    left[index].IsActivated != right[index].IsActivated || left[index].IsDestroyed != right[index].IsDestroyed)
                    return false;
            return true;
        }

        private void OnDestroy() => UnbindMatch();
    }
}
