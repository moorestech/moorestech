using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Client.Game.InGame.Entity.Removal;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Network.API.Requests.Removal;
using Game.Train.Unit;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class TrainCarRemovalRequestsTest
    {
        [SetUp]
        public void InitializeMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void TemporaryUnitTombstoneAndCarMovedToAnotherUnitDoNotConfirmRemoval()
        {
            var cache = new TrainUnitClientCache(RailGraphClientCache.CreateForEditorTest());
            var ticks = new TrainUnitTickState();
            var sender = new Sender();
            var service = new TrainCarRemovalRequests(cache, ticks, sender);
            var original = new TrainUnitInstanceId(Guid.NewGuid());
            var split = new TrainUnitInstanceId(Guid.NewGuid());
            var car = new TrainCarInstanceId(100);
            cache.Upsert(Snapshot(original, car));
            var results = new List<DeleteCompletion>();
            service.Remove(car, car).Subscribe(results.Add);

            // 同tickのdelete/upsert間に一時不在でも成功しない
            // Temporary absence between same-tick delete and upsert is not success
            cache.Remove(original);
            Assert.AreEqual(DeleteCompletionKind.Pending, results[^1].Kind);
            cache.Upsert(Snapshot(split, car));
            ticks.AdvanceTick();
            Assert.AreEqual(DeleteCompletionKind.Pending, results[^1].Kind);
            cache.Remove(split);
            Assert.AreEqual(DeleteCompletionKind.Pending, results[^1].Kind);
            ticks.AdvanceTick();
            Assert.AreEqual(DeleteCompletionKind.Succeeded, results[^1].Kind);
        }

        [Test]
        public void ConsecutiveInventoryFullDenialsEachSettleAndLaterRequestsStillRun()
        {
            var cache = new TrainUnitClientCache(RailGraphClientCache.CreateForEditorTest());
            var ticks = new TrainUnitTickState();
            var sender = new Sender();
            var service = new TrainCarRemovalRequests(cache, ticks, sender);
            var unit = new TrainUnitInstanceId(Guid.NewGuid());
            var first = new TrainCarInstanceId(100);
            var second = new TrainCarInstanceId(101);
            var third = new TrainCarInstanceId(102);
            cache.Upsert(Snapshot(unit, first, second, third));
            var firstResults = new List<DeleteCompletion>();
            var secondResults = new List<DeleteCompletion>();
            var thirdResults = new List<DeleteCompletion>();

            // 満杯で2台続けて撤去すると、どちらも自分の応答で拒否が確定する
            // Removing two cars in a row on a full inventory settles each by its own denial response
            service.Remove(first, first).Subscribe(firstResults.Add);
            service.Remove(second, second).Subscribe(secondResults.Add);
            Assert.AreEqual(2, sender.Sent.Count, "要求同士は互いを待たない / Requests must not wait on each other");
            sender.Respond(0, InventoryFull());
            sender.Respond(1, InventoryFull());
            Assert.AreEqual(DeleteCompletionKind.Rejected, firstResults[^1].Kind);
            Assert.AreEqual(DeleteCompletionKind.Rejected, secondResults[^1].Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.InventoryFull, secondResults[^1].DenyReason);

            // 応答の届かない要求があっても後続の撤去は送られ成功する
            // A request still awaiting its response does not hold later removals
            var held = new TrainCarInstanceId(103);
            cache.Upsert(Snapshot(unit, first, second, third, held));
            service.Remove(held, held).Subscribe(_ => { });
            service.Remove(third, third).Subscribe(thirdResults.Add);
            Assert.AreEqual(4, sender.Sent.Count);
            sender.Respond(3, new RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack(true, RemoveTrainCarProtocol.RemoveTrainCarFailureReason.None));
            cache.Upsert(Snapshot(unit, first, second, held));
            ticks.AdvanceTick();
            Assert.AreEqual(DeleteCompletionKind.Succeeded, thirdResults[^1].Kind);
        }

        private static RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack InventoryFull()
        {
            return new RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack(false, RemoveTrainCarProtocol.RemoveTrainCarFailureReason.PlayerInventoryFull);
        }

        private static TrainUnitSnapshotBundle Snapshot(TrainUnitInstanceId unit, params TrainCarInstanceId[] ids)
        {
            var cars = new List<TrainCarSnapshot>();
            foreach (var id in ids) cars.Add(new TrainCarSnapshot(id, Guid.Empty, true, 1));
            return new TrainUnitSnapshotBundle(new TrainSimulationSnapshot(unit, 0, 0, 0, 0, cars), null);
        }

        private sealed class Sender : ITrainCarRemovalSender
        {
            internal readonly List<TrainCarInstanceId> Sent = new();
            private readonly List<UniTaskCompletionSource<RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack>> _responses = new();

            public UniTask<RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack> RemoveTrain(TrainCarInstanceId id, System.Threading.CancellationToken ct)
            {
                Sent.Add(id);
                var source = new UniTaskCompletionSource<RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack>();
                _responses.Add(source);
                return source.Task;
            }

            internal void Respond(int index, RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack response) => _responses[index].TrySetResult(response);
        }
    }
}
