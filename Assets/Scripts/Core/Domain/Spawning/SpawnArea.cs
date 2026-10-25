// Unity XY 평면의 유한한 직사각형/점을 표현하며 Z는 표현 좌표로 분리합니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class SpawnArea
    {
        public BattlefieldPoint Center { get; }
        public double Width { get; }
        public double Height { get; }
        public double VisualZ { get; }
        public SpawnArea(BattlefieldPoint center, double width = 0, double height = 0, double visualZ = 0)
        {
            if (!Finite(width) || !Finite(height) || !Finite(visualZ) || width < 0 || height < 0 ||
                !Finite(center.X - width / 2) || !Finite(center.X + width / 2) ||
                !Finite(center.Y - height / 2) || !Finite(center.Y + height / 2))
                throw new ArgumentOutOfRangeException(nameof(width));
            Center = center; Width = width; Height = height; VisualZ = visualZ;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public string GetPayloadKey() => FormattableString.Invariant($"{Center.X:R},{Center.Y:R},{Width:R},{Height:R},{VisualZ:R}");
    }
}
