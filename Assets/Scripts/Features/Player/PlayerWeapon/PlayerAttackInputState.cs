/// <summary>마우스 공격 버튼의 연사 허용 상태입니다. 차단된 입력은 유효한 새 클릭이 있어야 재개합니다.</summary>
public sealed class PlayerAttackInputState
{
    private bool _active;

    public bool Read(bool held, bool pressedThisFrame, bool blocked)
    {
        if (!held || blocked) _active = false;
        else if (pressedThisFrame) _active = true;
        return _active;
    }

    public void Reset() => _active = false;
}
