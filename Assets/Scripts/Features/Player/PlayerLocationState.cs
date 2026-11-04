/*
 * Match에 사용되지 않는 우주선 내외부 판단 컴포넌트
 */
using System;
using UnityEngine;

public sealed class PlayerLocationState : MonoBehaviour
{
    private bool _isInSpaceship;
    public bool IsInSpaceship => _isInSpaceship;
    public event Action<bool> LocationChanged;

    public void CheckLocation(bool isin)
    {
        if (_isInSpaceship == isin)
            return;

        _isInSpaceship = isin;
        LocationChanged?.Invoke(isin);
    }
}
