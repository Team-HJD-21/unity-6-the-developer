using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    /// <summary>화면 전환 동안 HUD 표시만 보류합니다. 게임 상태·알림 수명·입력 권한은 변경하지 않습니다.</summary>
    public static class HudPresentationVisibility
    {
        private static readonly HashSet<Scope> Scopes = new();
        public static bool IsSuppressed => Scopes.Count > 0;

        public static IDisposable Suppress()
        {
            var scope = new Scope();
            Scopes.Add(scope);
            return scope;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Scopes.Clear();

        private sealed class Scope : IDisposable
        {
            public void Dispose() => Scopes.Remove(this);
        }
    }
}
