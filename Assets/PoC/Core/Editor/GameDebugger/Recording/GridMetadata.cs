// 기록 내보내기용 Grid 설정 복사본입니다. Core 설정을 변경하지 않습니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GridMetadata
    {
        public double CenterX, CenterY, Width, Height;
        public int CellsX, CellsY;
        public GridMetadata(BattlefieldGridConfiguration configuration)
        { CenterX=configuration.Origin.X; CenterY=configuration.Origin.Y; Width=configuration.MapWidth; Height=configuration.MapHeight; CellsX=configuration.CellsX; CellsY=configuration.CellsY; }
    }
}
