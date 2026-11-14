// 로컬 운영 화면 stack·포커스·Presenter와 입력 lease를 소유합니다.
// Match 결과는 현재 식별자에 한 번만 적용하고 Scene/Match 교체 시 오래된 화면을 닫습니다.
// Scene 전환·게임 pause·승패 판단은 이 객체가 담당하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class ScreenManager : IDisposable
    {
        private sealed class Entry
        {
            public ScreenState State;
            public IScreenPresenter Presenter;
            public IDisposable Input;
        }
        private readonly IInputBlocker _input;
        private readonly List<Entry> _screens = new List<Entry>();
        private Func<ScreenState, IScreenPresenter> _factory;
        private bool _disposed, _mutating;
        private long _sequence;
        public int Count => _screens.Count;
        public ScreenState Top => _screens.Count == 0 ? null : _screens[_screens.Count - 1].State;
        public MatchId BoundMatchId { get; private set; }
        public MatchCompletion LastCompletion { get; private set; }
        public ScreenManager(IInputBlocker input) => _input = input ?? throw new ArgumentNullException(nameof(input));
        public void SetPresenterFactory(Func<ScreenState, IScreenPresenter> factory)
        {
            ThrowIfUnavailable();
            if (_factory != null || Count != 0) throw new InvalidOperationException("Configure one Presenter factory before opening screens.");
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        public void BindMatch(MatchId match)
        {
            ThrowIfUnavailable();
            if (match.IsEmpty) throw new ArgumentException("A Match ID is required.", nameof(match));
            if (BoundMatchId == match) return;
            ResetContext(); BoundMatchId = match; LastCompletion = null;
        }
        public void ResetContext()
        {
            ThrowIfUnavailable(); BoundMatchId = default;
            var errors = new List<Exception>();
            foreach (Entry entry in new List<Entry>(_screens))
                if (entry.State.Kind != ScreenKind.Loading) Attempt(() => Close(entry.State.Id), errors);
            if (errors.Count > 0) throw new AggregateException("Screen context reset; some Presenters failed to close.", errors);
        }
        public ScreenState ShowMenu()
        {
            ThrowIfUnavailable();
            if (Top != null && Top.Kind != ScreenKind.Menu) return null;
            return Find(ScreenKind.Menu) ?? Open(ScreenKind.Menu);
        }
        public ScreenState ShowLoading() { ThrowIfUnavailable(); return Find(ScreenKind.Loading) ?? Open(ScreenKind.Loading); }
        public ScreenState ShowSettings()
        {
            ThrowIfUnavailable();
            if (Top != null && Top.Kind != ScreenKind.Menu && Top.Kind != ScreenKind.Settings) return null;
            return Find(ScreenKind.Settings) ?? Open(ScreenKind.Settings);
        }
        public bool ShowResult(MatchCompletion completion)
        {
            ThrowIfUnavailable();
            if (completion == null) throw new ArgumentNullException(nameof(completion));
            if (BoundMatchId.IsEmpty || completion.Result.MatchId != BoundMatchId) return false;
            if (LastCompletion != null && !ReferenceEquals(LastCompletion, completion)) return false;
            LastCompletion = completion;
            if (Find(ScreenKind.MatchResult) == null) Open(ScreenKind.MatchResult, completion);
            return true;
        }
        public ScreenState ShowRecovery()
        {
            ThrowIfUnavailable();
            if (BoundMatchId.IsEmpty || LastCompletion != null) return null;
            return Find(ScreenKind.MissionRecovery) ?? Open(ScreenKind.MissionRecovery);
        }
        public bool Back() { ThrowIfUnavailable(); return Top != null && Top.CanGoBack && Close(Top.Id); }
        public bool Close(long id)
        {
            if (_disposed) return false;
            ThrowIfUnavailable();
            int index = _screens.FindIndex(entry => entry.State.Id == id);
            if (index < 0) return false;
            var errors = new List<Exception>();
            _mutating = true;
            try
            {
                Entry entry = _screens[index]; bool wasTop = index == _screens.Count - 1;
                _screens.RemoveAt(index);
                Attempt(entry.Presenter.Dispose, errors); Attempt(entry.Input.Dispose, errors);
                if (wasTop && Count > 0) Attempt(() => _screens[Count - 1].Presenter.SetFocused(true), errors);
            }
            finally { _mutating = false; }
            if (errors.Count > 0) throw new AggregateException("Screen closed; Presenter cleanup failed.", errors);
            return true;
        }
        private ScreenState Find(ScreenKind kind)
        {
            foreach (Entry entry in _screens) if (entry.State.Kind == kind) return entry.State;
            return null;
        }
        private ScreenState Open(ScreenKind kind, MatchCompletion completion = null)
        {
            if (_factory == null) throw new InvalidOperationException("Configure the actual Presenter factory before opening a screen.");
            var state = new ScreenState(checked(++_sequence), kind, completion);
            var entry = new Entry { State = state };
            _mutating = true;
            try
            {
                entry.Input = _input.Acquire("Screen:" + kind + ":" + state.Id);
                entry.Presenter = _factory(state) ?? throw new InvalidOperationException("A screen Presenter is required.");
                bool behindLoading = Top?.Kind == ScreenKind.Loading && kind != ScreenKind.Loading;
                if (!behindLoading && Count > 0) _screens[Count - 1].Presenter.SetFocused(false);
                _screens.Insert(behindLoading ? Count - 1 : Count, entry);
                entry.Presenter.SetFocused(!behindLoading);
                return state;
            }
            catch (Exception error)
            {
                _screens.Remove(entry);
                var errors = new List<Exception> { error };
                if (entry.Presenter != null) Attempt(entry.Presenter.Dispose, errors);
                if (entry.Input != null) Attempt(entry.Input.Dispose, errors);
                if (Count > 0) Attempt(() => _screens[Count - 1].Presenter.SetFocused(true), errors);
                throw new AggregateException("Screen opening failed and was rolled back.", errors);
            }
            finally { _mutating = false; }
        }
        public void Dispose()
        {
            if (_disposed) return;
            ThrowIfUnavailable();
            var errors = new List<Exception>();
            foreach (Entry entry in new List<Entry>(_screens)) Attempt(() => Close(entry.State.Id), errors);
            _disposed = true; BoundMatchId = default; _factory = null;
            if (errors.Count > 0) throw new AggregateException("All screen cleanup attempted.", errors);
        }
        private void ThrowIfUnavailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ScreenManager));
            if (_mutating) throw new InvalidOperationException("Screen mutations cannot reenter Presenter setup or cleanup.");
        }
        private static void Attempt(Action action, List<Exception> errors)
        { try { action(); } catch (Exception error) { errors.Add(error); } }
    }
}
