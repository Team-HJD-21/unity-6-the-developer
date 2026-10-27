/// <summary>유효한 새 클릭으로만 연사를 시작하고, 차단되면 버튼을 놓을 때까지 재개하지 않습니다.</summary>
public sealed class PlayerAttackGesture
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
