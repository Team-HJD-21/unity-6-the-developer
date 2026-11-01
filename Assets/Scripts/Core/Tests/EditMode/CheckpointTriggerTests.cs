// 관찰 경계의 중복/과거/다른 Scope 요청·해제·늦은 IO·observer 오류 격리를 검증합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine.TestTools;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CheckpointTriggerTests
    {
        private static CheckpointRequest Save(CheckpointService service, long sequence, Guid request = default) =>
            new CheckpointRequest(service.MatchId, service.Epoch, request == Guid.Empty ? Guid.NewGuid() : request,
                sequence, CheckpointRequestKind.Save, new CheckpointSlot("explicit"));
        [Test] public void LatestDuplicateReturnsTheSameResultWithoutSavingAgain()
        {
            using var h = new CheckpointServiceTests.Harness(); h.Add("world", 1);
            var channel = new CheckpointRequestChannel(); using var trigger = new CheckpointTrigger(channel, h.Service);
            var results = new List<CheckpointOperationResult>(); trigger.Completed += (_, result) => results.Add(result);
            var request = Save(h.Service, 1); channel.Publish(request); channel.Publish(request);
            Assert.That(h.Store.Saves, Is.EqualTo(1)); Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[1].OperationId, Is.EqualTo(results[0].OperationId));
        }
        [Test] public void OlderSequenceAndConflictingLatestRequestCannotExecute()
        {
            using var h = new CheckpointServiceTests.Harness(); h.Add("world", 1);
            var channel = new CheckpointRequestChannel(); using var trigger = new CheckpointTrigger(channel, h.Service);
            CheckpointOperationResult last = null; trigger.Completed += (_, result) => last = result;
            channel.Publish(Save(h.Service, 1)); channel.Publish(Save(h.Service, 3)); channel.Publish(Save(h.Service, 2));
            Assert.That(last.Reason, Is.EqualTo("PastRequest")); channel.Publish(Save(h.Service, 3));
            Assert.That(last.Reason, Is.EqualTo("RequestConflict")); Assert.That(h.Store.Saves, Is.EqualTo(2));
        }
        [Test] public void RestoreEpochRejectsOldIntentWithoutResettingTheSourceSequence()
        {
            using var h = new CheckpointServiceTests.Harness(); h.Add("world", 1);
            var channel = new CheckpointRequestChannel(); using var trigger = new CheckpointTrigger(channel, h.Service);
            CheckpointOperationResult last = null; trigger.Completed += (_, result) => last = result;
            var old = Save(h.Service, 1); channel.Publish(old);
            h.Service.Restore(h.Target(("world", CheckpointSectionKind.World, 5)));
            channel.Publish(old); Assert.That(last.Reason, Is.EqualTo("DifferentMatchOrEpoch")); Assert.That(last.IsCurrentScope, Is.False);
            channel.Publish(Save(h.Service, 2)); Assert.That(h.Store.Saves, Is.EqualTo(2));
        }
        [Test] public void DisposedSubscriptionStopsRequestsAndOneBadObserverDoesNotHideOtherResults()
        {
            using var h = new CheckpointServiceTests.Harness(); h.Add("world", 1);
            var channel = new CheckpointRequestChannel(); using var trigger = new CheckpointTrigger(channel, h.Service);
            int observed = 0; trigger.Completed += (_, _) => throw new InvalidOperationException("observer failed");
            trigger.Completed += (_, _) => observed++;
            channel.Publish(Save(h.Service, 1)); Assert.That(observed, Is.EqualTo(1)); Assert.That(trigger.LastObserverError, Is.Not.Null);
            trigger.Dispose(); channel.Publish(Save(h.Service, 2)); Assert.That(h.Store.Saves, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator InFlightDuplicateAndDisposedTriggerDoNotStartOrPublishAnotherOperation()
        {
            using var h = new CheckpointServiceTests.Harness(); h.Add("world", 1); h.Store.Block = true;
            var channel = new CheckpointRequestChannel(); using var trigger = new CheckpointTrigger(channel, h.Service);
            int observed = 0; trigger.Completed += (_, _) => observed++;
            var request = Save(h.Service, 1); channel.Publish(request); channel.Publish(request);
            Assert.That(trigger.PendingRequests, Is.EqualTo(1)); Assert.That(h.Store.Saves, Is.EqualTo(1));
            trigger.Dispose(); h.Store.Completion.SetResult(true);
            var timeout = DateTime.UtcNow.AddSeconds(5); while (trigger.PendingRequests > 0 && DateTime.UtcNow < timeout) yield return null;
            Assert.That(trigger.PendingRequests, Is.Zero); Assert.That(observed, Is.Zero);
        }
    }
}
