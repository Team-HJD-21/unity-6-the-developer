// 동일한 지역 원본에서 정적 사용 제한과 동적 무잠금 규칙을 읽습니다.
// 실제 터렛 잠금·전력·공격 처리는 Feature가 수행합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class RegionAccessPolicy : IRegionAccessPolicy
    {
        public IRegionGridSource Regions { get; }
        public RegionAccessPolicy(IRegionGridSource regions)
            => Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        public bool CanUseAt(BattlefieldPoint position)
        {
            RegionGridSnapshot snapshot = Regions.Snapshot;
            if (snapshot.Mode == RegionPolicyMode.Dynamic) return true;
            return snapshot.Definition.TryGetCellIndex(position, out int cell) &&
                snapshot.IsRegionUnlocked(snapshot.Definition.RegionIds[cell]);
        }
    }
}
