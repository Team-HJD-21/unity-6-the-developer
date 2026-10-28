// 화면 stack·포커스·중복·오래된 결과와 Presenter 실패/입력 lease 정리를 검증합니다.
// 실제 UGUI 버튼/Scene 경로는 별도 통합 검사에서 확인합니다.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class ScreenManagerTests
    {
        private sealed class View : IScreenPresenter
        {
            public bool Focused, Disposed, FailDispose;
            public Action DuringDispose;
            public void SetFocused(bool focused) => Focused = focused;
            public void Dispose()
            {
                Disposed = true; DuringDispose?.Invoke();
                if (FailDispose) throw new InvalidOperationException("view");
            }
        }
        private static MatchCompletion Completion(string id) => new MatchCompletion(
            new MatchResult(new MatchId(id), MatchOutcome.Defeat, 1, DateTimeOffset.UtcNow),
            new RewardReceipt(new MatchId(id), MatchOutcome.Defeat, Array.Empty<CurrencyDelta>()));

        [Test]
        public void LoadingKeepsFocusAboveResultAndBackCannotDismissFinalOutcome()
        {
            using var input = new InputLockService(); using var screens = new ScreenManager(input);
            var views = new Dictionary<long, View>();
            screens.SetPresenterFactory(state => views[state.Id] = new View());
            screens.BindMatch(new MatchId("match"));
            var menu = screens.ShowMenu(); var loading = screens.ShowLoading();
            screens.ShowResult(Completion("match"));
            Assert.That(screens.Count, Is.EqualTo(3)); Assert.That(screens.Top, Is.SameAs(loading));
            Assert.That(views[menu.Id].Focused, Is.False); Assert.That(views[loading.Id].Focused, Is.True);
            Assert.That(screens.Back(), Is.False);
            screens.Close(loading.Id);
            Assert.That(screens.Top.Kind, Is.EqualTo(ScreenKind.MatchResult));
            Assert.That(views[screens.Top.Id].Focused, Is.True);
            Assert.That(screens.Back(), Is.False);
            Assert.That(screens.ShowMenu(), Is.Null);
        }

        [Test]
        public void DuplicateOpenAndResultDoNotCreateExtraPresentersOrInputLocks()
        {
            using var input = new InputLockService(); using var screens = new ScreenManager(input);
            int created = 0; screens.SetPresenterFactory(_ => { created++; return new View(); });
            var menu = screens.ShowMenu(); Assert.That(screens.ShowMenu(), Is.SameAs(menu));
            screens.Close(menu.Id); screens.BindMatch(new MatchId("match"));
            var result = Completion("match"); Assert.That(screens.ShowResult(result), Is.True);
            Assert.That(screens.ShowResult(result), Is.True);
            Assert.That(screens.ShowResult(Completion("other")), Is.False);
            Assert.That(created, Is.EqualTo(2)); Assert.That(input.Count, Is.EqualTo(1));
        }

        [Test]
        public void NewMatchClosesOldResultButKeepsLoadingLeaseAndRejectsLateResults()
        {
            using var input = new InputLockService(); using var screens = new ScreenManager(input);
            screens.SetPresenterFactory(_ => new View()); screens.BindMatch(new MatchId("first"));
            var result = Completion("first"); screens.ShowResult(result); var loading = screens.ShowLoading();
            screens.BindMatch(new MatchId("second"));
            Assert.That(screens.Count, Is.EqualTo(1)); Assert.That(screens.Top, Is.SameAs(loading));
            Assert.That(input.Count, Is.EqualTo(1)); Assert.That(screens.LastCompletion, Is.Null);
            Assert.That(screens.ShowResult(result), Is.False);
            screens.Close(loading.Id); Assert.That(input.Count, Is.Zero);
        }

        [Test]
        public void PresenterSetupFailureRollsBackOnlyItsOwnedLease()
        {
            using var input = new InputLockService(); using var external = input.Acquire("loading elsewhere");
            using var screens = new ScreenManager(input);
            screens.SetPresenterFactory(_ => throw new InvalidOperationException("factory"));
            Assert.Throws<AggregateException>(() => screens.ShowMenu());
            Assert.That(screens.Count, Is.Zero); Assert.That(input.Count, Is.EqualTo(1));
        }

        [Test]
        public void FailedPresenterDisposeCannotSkipOtherViewsOrLeaveLocks()
        {
            using var input = new InputLockService(); var screens = new ScreenManager(input);
            var views = new List<View>(); screens.SetPresenterFactory(_ => { var view = new View { FailDispose = true }; views.Add(view); return view; });
            screens.ShowMenu(); screens.ShowLoading();
            Assert.Throws<AggregateException>(screens.Dispose);
            Assert.That(screens.Count, Is.Zero); Assert.That(input.Count, Is.Zero);
            Assert.That(views.TrueForAll(view => view.Disposed), Is.True);
            screens.Dispose(); Assert.That(input.IsBlocked, Is.False);
        }

        [Test]
        public void PresenterCannotReenterScreenMutationDuringCleanup()
        {
            using var input = new InputLockService(); using var screens = new ScreenManager(input);
            screens.SetPresenterFactory(_ => new View { DuringDispose = () => screens.ShowMenu() });
            var menu = screens.ShowMenu(); Assert.Throws<AggregateException>(() => screens.Close(menu.Id));
            Assert.That(screens.Count, Is.Zero); Assert.That(input.Count, Is.Zero);
        }
    }
}
