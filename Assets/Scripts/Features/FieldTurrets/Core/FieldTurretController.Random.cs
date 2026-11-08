// 필드 무기의 산포 난수를 공용 Match 흐름에 연결합니다.
// 탄환/미사일의 조준·피해 규칙과 별도로 seed 및 진행 상태는 Core가 소유합니다.
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed partial class FieldTurretController : IRandomConsumer
    {
        internal IRandomSource CombatRandom { get; private set; }
        public IDisposable BindRandom(IRandomStreams random)
        {
            IRandomSource source = CombatRandom;
            var binding = RandomBinding.Bind(random, "combat/field-turret", ref source);
            CombatRandom = source;
            return binding;
        }
    }
}
