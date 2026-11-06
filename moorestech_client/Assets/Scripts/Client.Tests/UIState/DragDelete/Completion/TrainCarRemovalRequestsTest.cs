using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Entity.Removal;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Network.API;
using Client.Network.API.Requests.Removal;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UniRx;
using static Server.Protocol.PacketResponse.RemoveTrainCarProtocol;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class TrainCarRemovalRequestsTest
    {
        private static readonly TrainUnitInstanceId Unit = new(Guid.NewGuid());

        [SetUp]
        public void InitializeMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void ConsecutiveInventoryFullDenialsEachSettleWithoutWaitingOnEachOther()
        {
            var (service, sender) = CreateService(100, 101);
            var first = Track(service, 100);
            var second = Track(service, 101);

            // 満杯で2台続けて撤去すると、どちらも自分の応答で拒否が確定する
            // Removing two cars in a row on a full inventory settles each by its own denial
            Assert.AreEqual(2, sender.Sent.Count, "要求同士は互いを待たない / Requests must not wait on each other");
            sender.Respond(1, Response(false, RemoveTrainCarFailureReason.PlayerInventoryFull));
            sender.Respond(0, Response(false, RemoveTrainCarFailureReason.PlayerInventoryFull));
            AssertFinal(first, DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            AssertFinal(second, DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
        }

        [Test]
        public void LostResponseClosesAsRejectedAndDoesNotHoldLaterRequests()
        {
            var (service, sender) = CreateService(100, 101);
            var lost = Track(service, 100);
            var later = Track(service, 101);

            // 応答期限切れは不明な拒否で閉じ、後続の要求は独立に成功する
            // A timed-out response closes as an unknown denial and later requests succeed independently
            sender.Respond(1, Response(true, RemoveTrainCarFailureReason.None));
            AssertFinal(later, DeleteCompletionKind.Succeeded, null);
            Assert.AreEqual(DeleteCompletionKind.Pending, lost[^1].Kind);
            sender.Respond(0, (null, PacketWaitCompletionReason.Timeout));
            AssertFinal(lost, DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError);
        }

        [Test]
        public void ServerSideAbsentTargetSettlesAsSucceeded()
        {
            var (service, sender) = CreateService(100, 101);
            var trainMissing = Track(service, 100);
            var carMissing = Track(service, 101);

            // 他者が先に撤去した車両は撤去の目的を満たしているため成功とする
            // A car another player already removed meets the goal, so it settles as success
            sender.Respond(0, Response(false, RemoveTrainCarFailureReason.TrainNotFound));
            sender.Respond(1, Response(false, RemoveTrainCarFailureReason.CarNotFound));
            AssertFinal(trainMissing, DeleteCompletionKind.Succeeded, null);
            AssertFinal(carMissing, DeleteCompletionKind.Succeeded, null);
        }

        [Test]
        public void TargetAbsentFromClientCacheIsRejectedWithoutSending()
        {
            var (service, sender) = CreateService(100);
            var results = Track(service, 999);
            Assert.AreEqual(0, sender.Sent.Count);
            AssertFinal(results, DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError);
        }

        private static (TrainCarRemovalRequests service, Sender sender) CreateService(params long[] carIds)
        {
            var cache = new TrainUnitClientCache(RailGraphClientCache.CreateForEditorTest());
            var cars = new List<TrainCarSnapshot>();
            foreach (var id in carIds) cars.Add(new TrainCarSnapshot(new TrainCarInstanceId(id), Guid.Empty, true, 1));
            cache.Upsert(new TrainUnitSnapshotBundle(new TrainSimulationSnapshot(Unit, 0, 0, 0, 0, cars), null));
            var sender = new Sender();
            return (new TrainCarRemovalRequests(cache, sender), sender);
        }

        private static List<DeleteCompletion> Track(TrainCarRemovalRequests service, long carId)
        {
            var results = new List<DeleteCompletion>();
            service.Remove(new TrainCarInstanceId(carId), carId).Subscribe(results.Add);
            return results;
        }

        private static void AssertFinal(List<DeleteCompletion> results, DeleteCompletionKind kind, LocalizationKey? reason)
        {
            Assert.AreEqual(kind, results[^1].Kind);
            Assert.AreEqual(reason, results[^1].DenyReason);
        }

        private static (RemoveTrainCarResponseMessagePack, PacketWaitCompletionReason) Response(bool isSuccess, RemoveTrainCarFailureReason reason)
        {
            return (new RemoveTrainCarResponseMessagePack(isSuccess, reason), PacketWaitCompletionReason.Received);
        }

        private sealed class Sender : ITrainCarRemovalSender
        {
            internal readonly List<TrainCarInstanceId> Sent = new();
            private readonly List<UniTaskCompletionSource<(RemoveTrainCarResponseMessagePack, PacketWaitCompletionReason)>> _responses = new();

            public UniTask<(RemoveTrainCarResponseMessagePack response, PacketWaitCompletionReason reason)> RemoveTrain(TrainCarInstanceId id, CancellationToken ct)
            {
                Sent.Add(id);
                var source = new UniTaskCompletionSource<(RemoveTrainCarResponseMessagePack, PacketWaitCompletionReason)>();
                _responses.Add(source);
                return source.Task;
            }

            internal void Respond(int index, (RemoveTrainCarResponseMessagePack, PacketWaitCompletionReason) response) => _responses[index].TrySetResult(response);
        }
    }
}
