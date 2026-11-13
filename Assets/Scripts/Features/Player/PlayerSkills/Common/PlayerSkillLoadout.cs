// Q/E 준비 데이터를 소유합니다. 기존 순차 선택과 명시적 슬롯 교체/교환을 함께 제공합니다.
using System;

public enum PlayerSkillId { None = 0, Bomb = 1, PlayerTurret = 2, EnergyBarrier = 3, LureBeacon = 4 }

public sealed class PlayerSkillLoadout
{
    public PlayerSkillId Q { get; private set; }
    public PlayerSkillId E { get; private set; }
    public event Action Changed;

    public bool Contains(PlayerSkillId skill) => skill != PlayerSkillId.None && (Q == skill || E == skill);

    public bool TrySelect(PlayerSkillId skill)
    {
        if ((int)skill < (int)PlayerSkillId.Bomb || (int)skill > (int)PlayerSkillId.LureBeacon || Contains(skill)) return false;
        if (Q == PlayerSkillId.None) Q = skill;
        else if (E == PlayerSkillId.None) E = skill;
        else return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>0=Q, 1=E. 다른 슬롯의 스킬은 교환하고 새 스킬은 대상만 교체합니다. 같은 슬롯은 변경하지 않습니다.</summary>
    public bool TryEquip(int slot, PlayerSkillId skill)
    {
        if (slot < 0 || slot > 1 || (int)skill < (int)PlayerSkillId.Bomb || (int)skill > (int)PlayerSkillId.LureBeacon) return false;
        var current = slot == 0 ? Q : E;
        if (current == skill) return false;
        if (slot == 0)
        {
            if (E == skill) E = current;
            Q = skill;
        }
        else
        {
            if (Q == skill) Q = current;
            E = skill;
        }
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (Q == PlayerSkillId.None && E == PlayerSkillId.None) return;
        Q = E = PlayerSkillId.None;
        Changed?.Invoke();
    }

    /// <summary>Player 스킬 writer가 검증된 Q/E 선택을 한 번에 복원합니다. 선택 제한·사용 비용을 다시 실행하지 않습니다.</summary>
    public void Restore(PlayerSkillId q, PlayerSkillId e)
    {
        if (!Enum.IsDefined(typeof(PlayerSkillId), q) || !Enum.IsDefined(typeof(PlayerSkillId), e) ||
            (q != PlayerSkillId.None && q == e))
            throw new ArgumentException("Invalid saved Q/E skill selection.");
        if (Q == q && E == e) return;
        Q = q; E = e; Changed?.Invoke();
    }
}
