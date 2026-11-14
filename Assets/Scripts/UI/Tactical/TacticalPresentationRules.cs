using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

/// <summary>실제 스냅샷의 표시 의미만 정합니다. 해금·점유·활성 명령을 확정하지 않습니다.</summary>
public static class TacticalPresentationRules
{
    public enum TurretState { Active, Off, Locked, Cooling, NoPower, Destroyed, Busy, Unavailable }
    public enum CellState { Unoccupied, Available, Capturing, Captured }

    /// <summary>첫 연결/최대 체력 변경은 피격으로 오인하지 않고 실제 감소만 흔들림과 잔상으로 표시합니다.</summary>
    public sealed class Vitality
    {
        public int Health { get; private set; }
        public int Maximum { get; private set; }
        public float HitAt { get; private set; } = -100;
        private bool _bound;
        private float _trail;
        public void Sample(int health,int maximum,float now)
        {
            health=Mathf.Max(0,health);maximum=Mathf.Max(1,maximum);
            if(!_bound || maximum!=Maximum){_trail=health;HitAt=-100;}
            else if(health<Health){_trail=Mathf.Max(Health,Trail(now)*maximum);HitAt=now;}
            else if(health>Health)_trail=Mathf.Max(health,Trail(now)*maximum);
            Health=health;Maximum=maximum;_bound=true;
        }
        public float Fraction=>Health/(float)Mathf.Max(1,Maximum);
        public float Trail(float now)=>Mathf.Lerp(_trail,Health,Mathf.SmoothStep(0,1,Mathf.Clamp01((now-HitAt-.18f)/.65f)))/Mathf.Max(1,Maximum);
        public float Impact(float now)=>Mathf.Clamp01(1-(now-HitAt)/.42f);
        public Vector2 Offset(float now)
        {float t=now-HitAt,a=Impact(now);return new Vector2(Mathf.Sin(t*92)*3.5f,Mathf.Sin(t*71)*1.8f)*a*a;}
    }

    public enum FieldState { Hidden, Outside, Capturing, Paused, LeftCapture, Locked, Contested }
    public static bool CanReach(bool inputAvailable,bool mapOpen,bool captured,float distance,float nearDistance)
        => inputAvailable && (mapOpen ? captured : distance<=nearDistance);
    public static FieldState Field(RegionGridSnapshot regions,BattlefieldPoint player,StaticRegionCaptureSnapshot progress,bool justLeft)
    {
        if(regions==null)return FieldState.Hidden;
        if(justLeft)return FieldState.LeftCapture;
        if(regions.Definition.TryGetCellIndex(player,out int index))
        {
            if(regions.CapturedCells[index])return FieldState.Hidden;
            if(regions.Mode==RegionPolicyMode.Static && regions.Definition.CapturableCells[index] &&
                !regions.IsRegionUnlocked(regions.Definition.RegionIds[index]))return FieldState.Locked;
            if(progress!=null && !progress.IsSecured && progress.RegionId==regions.Definition.RegionIds[index])
            {
                if(progress.Reason==StaticRegionCaptureReason.Holding)return FieldState.Capturing;
                if(progress.Reason==StaticRegionCaptureReason.EnemyPressure)return FieldState.Contested;
                if(progress.HoldSeconds>0)return FieldState.Paused;
            }
        }
        return FieldState.Outside;
    }
    public static string FieldCaption(FieldState state)=>state switch
    {FieldState.Capturing=>"구역 확보 중",FieldState.Paused=>"확보 일시 중단",FieldState.LeftCapture=>"확보 구역 이탈",FieldState.Locked=>"잠긴 구역",FieldState.Contested=>"적 우세 · 확보 중단",FieldState.Outside=>"미점유 지역",_=>""};

    public static TurretState State(TurretPresentationSnapshot row, int availablePower, bool pending = false)
    {
        if (row == null) return TurretState.Unavailable;
        if (row.IsDestroyed) return TurretState.Destroyed;
        if (row.IsLocked) return TurretState.Locked;
        if (!row.IsInitialized) return TurretState.Unavailable;
        if (pending) return TurretState.Busy;
        if (row.IsCoolingDown) return TurretState.Cooling;
        if (row.IsActivated) return row.IsOperational ? TurretState.Active : TurretState.Busy;
        if (row.IsManualOperationBlocked) return TurretState.Busy;
        return availablePower < row.Power ? TurretState.NoPower : TurretState.Off;
    }

    public static string ToggleCaption(TurretPresentationSnapshot row, int availablePower, bool pending, bool territoryLocked)
    {
        if (row == null) return "확인 중";
        if (row.IsDestroyed) return "파괴됨";
        if (!row.IsInitialized) return "확인 중";
        if (pending) return "처리 중";
        if (row.IsLocked || territoryLocked) return "잠김";
        if (row.IsCoolingDown) return "냉각";
        if (row.IsManualOperationBlocked) return "조작 대기";
        if (row.IsActivated) return "끄기";
        return availablePower < row.Power ? "전력 부족" : "켜기";
    }

    public static CellState Cell(RegionGridSnapshot regions, int index, StaticRegionCaptureSnapshot progress)
    {
        if (regions == null || index < 0 || index >= regions.CapturedCells.Count) return CellState.Unoccupied;
        if (regions.CapturedCells[index]) return CellState.Captured;
        if (!regions.Definition.CapturableCells[index]) return CellState.Unoccupied;
        bool available = regions.Mode == RegionPolicyMode.Dynamic || regions.IsRegionUnlocked(regions.Definition.RegionIds[index]);
        if (!available) return CellState.Unoccupied;
        // 진행 중 멈춘 시도도 유지 중인 실제 누적이 있으면 구분합니다. UI가 시간을 누적하지 않습니다.
        if (regions.Mode == RegionPolicyMode.Static && progress != null && !progress.IsSecured &&
            progress.RegionId == regions.Definition.RegionIds[index] && (progress.HoldSeconds > 0 || progress.Reason == StaticRegionCaptureReason.Holding))
            return CellState.Capturing;
        return CellState.Available;
    }

    public static string Caption(TurretState state) => state switch
    {
        TurretState.Active => "가동", TurretState.Off => "대기", TurretState.Locked => "잠김",
        TurretState.Cooling => "냉각", TurretState.NoPower => "전력 부족", TurretState.Destroyed => "파괴",
        TurretState.Busy => "조작 대기", _ => "사용 불가"
    };

    public static Vector2 DragPan(Vector2 screenDelta, float halfHeight, float viewportPixelHeight)
        => -screenDelta * (halfHeight * 2 / Mathf.Max(1, viewportPixelHeight));
}
