// Publishes immutable match facts to listeners within one match lifetime.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchEventBus : IMatchEventStream, IDisposable
    {
        private readonly List<Action<MatchEvent>> _listeners = new List<Action<MatchEvent>>();
        private bool _isDisposed;

        public IDisposable Subscribe(Action<MatchEvent> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            ThrowIfDisposed();
            _listeners.Add(listener);
            return new Subscription(this, listener);
        }

        public void Publish(MatchEvent matchEvent)
        {
            if (matchEvent == null) throw new ArgumentNullException(nameof(matchEvent));
            ThrowIfDisposed();
            var listeners = _listeners.ToArray();
            foreach (var listener in listeners) listener(matchEvent);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _listeners.Clear();
        }

        private void Unsubscribe(Action<MatchEvent> listener)
        {
            if (!_isDisposed) _listeners.Remove(listener);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MatchEventBus));
        }

        private sealed class Subscription : IDisposable
        {
            private MatchEventBus _owner;
            private readonly Action<MatchEvent> _listener;

            public Subscription(MatchEventBus owner, Action<MatchEvent> listener)
            {
                _owner = owner;
                _listener = listener;
            }

            public void Dispose()
            {
                if (_owner == null) return;
                _owner.Unsubscribe(_listener);
                _owner = null;
            }
        }
    }
}
