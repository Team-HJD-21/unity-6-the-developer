// 창과 Scene Overlay가 공유하는 Editor 전용 표시 옵션입니다.
// 게임 정책·저장 데이터에는 영향을 주지 않습니다.
using System;

namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class BattlefieldDebugDisplaySettings
    {
        public bool IsEnabled;
        public bool ShowTerritory = true;
        public bool ShowFrontline = true;
        public bool ShowVertexLabels;
        public bool ShowGrid = true;
        public bool ShowOccupancy = true;
        public bool IsCellPickingEnabled;
        public bool IsCaptureMode;
    }
}
