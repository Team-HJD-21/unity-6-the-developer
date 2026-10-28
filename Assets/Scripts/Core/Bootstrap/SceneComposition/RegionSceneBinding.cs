// Scene의 D 터렛을 Match 소유 지역 Grid에 연결합니다.
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
    public sealed class RegionSceneBinding : MonoBehaviour
    {
        [SerializeField] private FieldTurretController _controller;
        [SerializeField] private Transform _controlUnit;
        [SerializeField] private RegionPolicyMode _initialMode = RegionPolicyMode.Static;
        private MatchSession _session;
        private IRegionGridSource _source;
        private RegionGridDefinition _definition;
        private readonly DynamicRegionCapturePolicy _dynamicPolicy = new DynamicRegionCapturePolicy();
        private IReadOnlyList<RegionTurretInput> _lastInputs;
        private Func<bool> _mayWrite;
        private float _nextSample;
        private bool _dirty;
        private int _zoneCount;
        public FieldTurretController Controller => _controller;
        public Transform ControlUnit => _controlUnit;
        public RegionPolicyMode InitialMode => _initialMode;
        public IRegionGridSource Source => _session != null && !_session.IsDisposed ? _source : null;
        public RegionCaptureEvaluation LastEvaluation { get; private set; }
        public bool HasWriteAuthority => _session != null && !_session.IsDisposed &&
            (_session.State.Phase == MatchPhase.Preparing || _session.State.Phase == MatchPhase.Running) &&
            _mayWrite != null && _mayWrite();

        public void BindMatch(MatchSession session, Func<bool> mayWrite = null)
        {
            if (_session != null) throw new InvalidOperationException("Region scene is already bound.");
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (_controller == null)
                throw new InvalidOperationException("Assign FieldTurretController to RegionSceneBinding.");
            RegionGridDefinition definition = CreateDefinition(_initialMode, out int zoneCount);
            _source = session.InitializeRegions(definition, _initialMode);
            _session = session;
            _definition = definition;
            _zoneCount = zoneCount;
            _mayWrite = mayWrite ?? (() => true);
            _session.Disposing += UnbindMatch;
            try
            {
                if (HasWriteAuthority)
                {
                    if (_initialMode == RegionPolicyMode.Static) ApplyStatic(1);
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
            if (_session == null) return;
            _session.Disposing -= UnbindMatch;
            FieldTurretInstanceRegistry.SnapshotChanged -= OnTurretChanged;
            if (_controller != null) _controller.SetRegionAccessPolicy(null);
            _session = null;
            _source = null;
            _lastInputs = null;
            _mayWrite = null;
            LastEvaluation = null;
        }

        public bool ResetPolicy(RegionPolicyMode mode, bool resetConfirmed)
        {
            if (!resetConfirmed || !HasWriteAuthority) return false;
            RegionGridDefinition definition = CreateDefinition(mode, out int zoneCount);
            RegionCaptureEvaluation evaluation = null;
            IReadOnlyList<int> cells;
            if (mode == RegionPolicyMode.Dynamic)
            {
                evaluation = _dynamicPolicy.Evaluate(definition, FieldTurretRegionGridAdapter.CaptureTurrets(_controller));
                cells = evaluation.CellIndices;
            }
            else if (mode == RegionPolicyMode.Static) cells = StaticCells(definition, 1);
            else throw new ArgumentOutOfRangeException(nameof(mode));
            // 새 정의와 초기 결과를 한 번에 발행합니다. 빈 정적 중간 상태로 터렛을 모두 끄지 않습니다.
            _session.ResetRegions(definition, mode, cells);
            _definition = definition;
            _zoneCount = zoneCount;
            LastEvaluation = evaluation;
            _lastInputs = null;
            _dirty = true;
            return true;
        }

        public bool TryUnlockThroughZone(int zone)
        {
            if (!HasWriteAuthority || _source.Snapshot.Mode != RegionPolicyMode.Static || zone < 1 || zone > _zoneCount)
                return false;
            _session.UpdateRegionCapture(_source.Snapshot, StaticCells(_definition, zone), RegionCaptureUpdateMode.Union);
            return true;
        }

        private void ApplyStatic(int zone)
            => _session.UpdateRegionCapture(_source.Snapshot, StaticCells(_definition, zone), RegionCaptureUpdateMode.Replace);

        private RegionGridDefinition CreateDefinition(RegionPolicyMode mode, out int zoneCount)
        {
            zoneCount = 0;
            if (mode == RegionPolicyMode.Dynamic)
                return FieldTurretRegionGridAdapter.CreateDefinition(_controller.CreateUnlockGridConfiguration());
            if (_controlUnit == null) throw new InvalidOperationException("Static regions require a CU Transform.");
            FieldTurretUnlockZones zones = _controller.CreateUnlockZoneDefinition(_controlUnit.position);
            zoneCount = zones.ZoneCount;
            return FieldTurretRegionGridAdapter.CreateDefinition(zones);
        }

        private static IReadOnlyList<int> StaticCells(RegionGridDefinition definition, int zone)
        {
            var cells = new List<int>();
            for (int index = 0; index < definition.RegionIds.Count; index++)
                if (definition.RegionIds[index] > 0 && definition.RegionIds[index] <= zone) cells.Add(index);
            return cells.AsReadOnly();
        }

        private void OnTurretChanged(int instanceId) => _dirty = true;

        private void Update()
        {
            if (!HasWriteAuthority || _source.Snapshot.Mode != RegionPolicyMode.Dynamic ||
                _session.State.Phase != MatchPhase.Running) return;
            if (!_dirty && Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + 0.1f;
            RefreshDynamic(false);
        }

        private void RefreshDynamic(bool force)
        {
            IReadOnlyList<RegionTurretInput> inputs = FieldTurretRegionGridAdapter.CaptureTurrets(_controller);
            _dirty = false;
            if (!force && SameInputs(inputs, _lastInputs)) return;
            RegionGridSnapshot basis = _source.Snapshot;
            RegionCaptureEvaluation evaluation = _dynamicPolicy.Evaluate(_definition, inputs);
            RegionUpdateStatus result = _session.UpdateRegionCapture(basis, evaluation.CellIndices, RegionCaptureUpdateMode.Replace);
            if (result != RegionUpdateStatus.Applied && result != RegionUpdateStatus.Unchanged)
                throw new InvalidOperationException("Region input no longer belongs to the current grid revision.");
            _lastInputs = inputs;
            LastEvaluation = evaluation;
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
