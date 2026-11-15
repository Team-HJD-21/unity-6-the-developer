using System;
using System.Reflection;

/// <summary>독립 전투 fixture의 후보 준비만 우주선 위치에서 수행하고 기존 위치를 복구합니다.</summary>
internal sealed class PlayerSkillResearchTestHub : IDisposable
{
    private static readonly FieldInfo Hub = typeof(PlayerControlState).GetField("_hubOnly", BindingFlags.Instance | BindingFlags.NonPublic);
    private PlayerControlState _controls;
    private readonly bool _previous;
    public PlayerSkillResearchTestHub(PlayerSkillResearch research)
    {
        _controls = research.GetComponent<PlayerControlState>();
        _previous = (bool)Hub.GetValue(_controls);
        Hub.SetValue(_controls, true);
    }
    public static bool Assign(PlayerSkillResearch research, int slot, PlayerSkillId skill)
    {
        using var hub = new PlayerSkillResearchTestHub(research);
        return research.TrySetCandidate(slot, skill);
    }
    public void Dispose()
    {
        if (_controls == null) return;
        Hub.SetValue(_controls, _previous); _controls = null;
    }
}
