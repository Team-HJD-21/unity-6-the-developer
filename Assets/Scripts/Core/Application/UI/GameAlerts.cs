// HUD/Feature의 짧은 로컬 피드백을 현재 App의 AlertService로 전달합니다.
// 초기화 전·종료 후에는 false를 반환하며 Scene 검색이나 임시 UI를 만들지 않습니다.
using System;

namespace TeamHJD.Game.Application
{
    public static class GameAlerts
    {
        private static AlertService _current;

        /// <summary>버튼·스킬 사용 결과에서 호출합니다. key는 같은 의미의 반복 메시지를 구분하는 선택 값입니다.</summary>
        public static bool Show(string message, AlertTone tone = AlertTone.Error, string key = null)
            => _current != null && _current.Show(message, tone, key);

        /// <summary>App 조립부 전용입니다. 반환 scope만 해제하며 이전 App의 정리가 새 바인딩을 지우지 않습니다.</summary>
        public static IDisposable Bind(AlertService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _current = service;
            return new Binding(service);
        }

        private sealed class Binding : IDisposable
        {
            private AlertService _service;
            public Binding(AlertService service) => _service = service;
            public void Dispose()
            {
                if (_service == null) return;
                if (ReferenceEquals(_current, _service)) _current = null;
                _service = null;
            }
        }
    }
}
