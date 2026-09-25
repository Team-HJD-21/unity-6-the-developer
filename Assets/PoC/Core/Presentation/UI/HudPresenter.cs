// Match Snapshot을 HUD View에 전달하고 Match 이벤트가 발생하면 화면을 갱신합니다.

using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Presentation.UI
{
    public sealed class HudPresenter : IDisposable
    {
        private readonly IHudView _view;
        private MatchSession _session;
        private IDisposable _eventSubscription;

        public HudPresenter(IHudView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Bind(MatchSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            Unbind();
            _session = session;
            _eventSubscription = session.Events.Subscribe(OnMatchEvent);
            Refresh(0);
        }

        public void Dispose() => Unbind();

        private void OnMatchEvent(MatchEvent matchEvent) => Refresh(matchEvent.Sequence);

        private void Refresh(long sequence)
        {
            if (_session == null) return;
            _view.Render(_session.CreateSnapshot(sequence));
        }

        private void Unbind()
        {
            _eventSubscription?.Dispose();
            _eventSubscription = null;
            _session = null;
        }
    }
}
