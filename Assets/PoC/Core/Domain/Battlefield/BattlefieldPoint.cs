// 엔진 좌표와 분리된 전장 평면의 2차원 위치 값입니다.

using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct BattlefieldPoint : IEquatable<BattlefieldPoint>
    {
        public double X { get; }
        public double Y { get; }

        public BattlefieldPoint(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(nameof(x));
            if (double.IsNaN(y) || double.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(y));
            X = x;
            Y = y;
        }

        public bool Equals(BattlefieldPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is BattlefieldPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public static bool operator ==(BattlefieldPoint left, BattlefieldPoint right) => left.Equals(right);
        public static bool operator !=(BattlefieldPoint left, BattlefieldPoint right) => !left.Equals(right);
    }
}
