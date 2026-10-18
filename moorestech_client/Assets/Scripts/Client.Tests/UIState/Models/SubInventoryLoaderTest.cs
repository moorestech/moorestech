using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Util.MessagePack;
using UniRx;

namespace Client.Tests.UIState.Models
{
    public class SubInventoryLoaderTest
    {
        [Test]
        public void InitialLoadRefreshAndRepeatedRefreshRefetchSerially()
        {
            var query = new ControlledQuery();
            var source = new TrainSubInventorySource(new TrainCarInstanceId(1), null);
            var loader = new SubInventoryLoader(query, source, CancellationToken.None);
            var published = new List<SubInventoryModel>();
            loader.OnLoaded.Subscribe(published.Add);
            loader.RequestRefresh();
            loader.RequestRefresh();
            loader.RequestRefresh();
            Assert.AreEqual(1, query.RequestCount);

            // 古い初回応答を返す前に通知を送り、表示せず取り直す
            // Notify before returning the stale initial response, then refetch without publishing it
            query.Complete(Model(1));
            Assert.AreEqual(2, query.RequestCount);
            Assert.AreEqual(0, published.Count);
            loader.RequestRefresh();
            loader.RequestRefresh();
            query.Complete(Model(1));
            Assert.AreEqual(3, query.RequestCount);
            var latest = Model(1);
            query.Complete(latest);
            Assert.AreEqual(1, published.Count);
            Assert.AreSame(latest, published[0]);
            Assert.IsFalse(loader.IsLoading);
            Assert.AreEqual(1, query.MaximumActiveRequests);
        }

        [Test]
        public void ClosingAndOpeningAnotherCarCannotApplyTheOldResponse()
        {
            var query = new ControlledQuery();
            using var closed = new CancellationTokenSource();
            var first = new SubInventoryLoader(query, new TrainSubInventorySource(new TrainCarInstanceId(1), null), closed.Token);
            var published = new List<SubInventoryModel>();
            first.OnLoaded.Subscribe(published.Add);
            first.RequestRefresh();
            first.RequestRefresh();
            closed.Cancel();
            var second = new SubInventoryLoader(query, new TrainSubInventorySource(new TrainCarInstanceId(2), null), CancellationToken.None);
            second.OnLoaded.Subscribe(published.Add);
            second.RequestRefresh();

            // 閉じた車両の応答が後着しても次の画面を変えない
            // A late response for the closed car must not change the next screen
            query.Complete(Model(1));
            Assert.AreEqual(0, published.Count);
            Assert.AreEqual(2, query.RequestCount);
            var current = Model(2);
            query.Complete(current);
            Assert.AreSame(current, published[0]);
            Assert.IsFalse(second.Matches(InventoryIdentifierMessagePack.CreateTrainMessage(1)));
            Assert.IsTrue(second.Matches(InventoryIdentifierMessagePack.CreateTrainMessage(2)));
            Assert.IsFalse(second.Matches(InventoryIdentifierMessagePack.CreateBlockMessage(UnityEngine.Vector3Int.zero)));
        }

        private static SubInventoryModel Model(long carId) => new(new TrainInventorySubInventoryIdentifier(carId));

        private sealed class ControlledQuery : ISubInventoryQuery
        {
            private readonly Queue<UniTaskCompletionSource<SubInventoryModel>> _pending = new();
            public int RequestCount { get; private set; }
            public int MaximumActiveRequests { get; private set; }

            public UniTask<SubInventoryModel> Load(ISubInventorySource source, CancellationToken ct)
            {
                RequestCount++;
                var completion = new UniTaskCompletionSource<SubInventoryModel>();
                _pending.Enqueue(completion);
                MaximumActiveRequests = System.Math.Max(MaximumActiveRequests, _pending.Count);
                return completion.Task;
            }

            public void Complete(SubInventoryModel model)
            {
                _pending.Dequeue().TrySetResult(model);
            }
        }
    }
}
