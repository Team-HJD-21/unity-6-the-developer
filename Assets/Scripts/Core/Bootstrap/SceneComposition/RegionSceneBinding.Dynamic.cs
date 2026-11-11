// CU 공간 노드와 시작 셀 설정을 동적 지역 평가에 연결합니다.
// 런타임 변경은 Host의 명시 Reset으로만 적용하고 잘못된 입력은 기존 상태를 보존합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class RegionSceneBinding
    {
        private BattlefieldPoint ControlUnitPosition()
        {
            if (_controlUnit == null) throw new InvalidOperationException("Assign the Scene CU before evaluating dynamic regions.");
            Vector3 position = _controlUnit.position;
            return new BattlefieldPoint(position.x, position.y);
        }

        private RegionCaptureEvaluation EvaluateDynamic(RegionGridDefinition definition, int[] initialCells)
            => _dynamicPolicy.EvaluateConnected(definition,
                FieldTurretRegionGridAdapter.CaptureTurrets(_controller, _entityIds), ControlUnitPosition(),
                initialCells);

        /// <summary>Host의 맵/디버그 도구가 시작 셀을 명시적으로 적용합니다. null/빈 목록은 CU 1셀 기본값입니다.</summary>
        /// <remarks>승인된 Reset에서만 새 Grid 세대로 적용합니다. 입력 오류는 현재 점유와 설정을 변경하지 않습니다.</remarks>
        public bool TrySetDynamicStartCells(IReadOnlyList<int> cells, bool resetConfirmed, out string error)
        {
            error = null;
            if (!resetConfirmed || !HasWriteAuthority || _source.Snapshot.Mode != RegionPolicyMode.Dynamic)
            {
                error = "현재 Host의 Dynamic Match에서 Reset을 확인한 뒤 적용하세요.";
                return false;
            }
            if (cells != null && cells.Count > _definition.Geometry.TotalCellCount)
            {
                error = "시작 셀 목록이 현재 Grid 셀 수보다 큽니다.";
                return false;
            }
            int[] requested = null;
            if (cells != null && cells.Count > 0)
            {
                var unique = new SortedSet<int>(cells);
                requested = new int[unique.Count];
                unique.CopyTo(requested);
            }
            RegionCaptureEvaluation evaluation;
            try
            {
                // null은 이전 override가 아니라 기본 CU 셀을 뜻하므로 여기서는 직접 전달합니다.
                evaluation = EvaluateDynamic(_definition, requested);
            }
            catch (ArgumentException invalid)
            {
                error = invalid.Message;
                return false;
            }
            _dynamicInitialCells = requested;
            _lastInputs = null;
            _lastControlUnitPosition = null;
            LastEvaluation = evaluation;
            LastDynamicError = null;
            _dirty = true;
            _session.ResetRegions(_definition, RegionPolicyMode.Dynamic, evaluation.CellIndices);
            return true;
        }

        /// <summary>설정 도구/저장 실행기가 현재 override를 복사합니다. 빈 배열은 CU 1셀 자동 기본값입니다.</summary>
        public int[] CaptureDynamicStartCells() => _dynamicInitialCells == null ? Array.Empty<int>() : (int[])_dynamicInitialCells.Clone();

        /// <summary>조립부가 모든 actor 복원을 마친 뒤 호출합니다. 복원된 CU·터렛·시작 셀로 최종 점유를 다시 확정합니다.</summary>
        public void RefreshDynamicAfterRestore()
        {
            if (HasWriteAuthority && _source.Snapshot.Mode == RegionPolicyMode.Dynamic) RefreshDynamic(true);
        }

    }
}
