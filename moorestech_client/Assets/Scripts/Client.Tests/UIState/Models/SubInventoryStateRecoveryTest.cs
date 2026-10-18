using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Block.Interface.State;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState.Models
{
    public class SubInventoryStateRecoveryTest
    {
        private SubInventoryState _state;
        private SubInventoryLoader _loader;
        private ControlledQuery _query;
        private CancellationTokenSource _cts;
        private readonly TrainInventorySubInventoryIdentifier _identifier = new(1);
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _query = new ControlledQuery();
            _cts = new CancellationTokenSource();
            var source = new TrainSubInventorySource(new TrainCarInstanceId(1), null);
            _loader = new SubInventoryLoader(_query, source, _cts.Token);

            // 接続起動だけ省略し、製品Stateの受信処理とモデル適用を使う
            // Skip connection boot only; use the production State receiver and model application
            _state = (SubInventoryState)FormatterServices.GetUninitializedObject(typeof(SubInventoryState));
            SetField("_loader", _loader);
            SetField("_loadInventoryCts", _cts);
            SetField("_onSubInventoryUpdated", new Subject<Unit>());
            SetField("_localPlayerInventoryController", new LocalPlayerInventoryController(new LocalPlayerInventory(), null));
            _loader.OnSubInventoryLoaded.Subscribe(model => typeof(SubInventoryState).GetMethod("ApplyInventory", PrivateInstance).Invoke(_state, new object[] { model }));
            _loader.RequestRefresh();

            #region Internal

            void SetField(string name, object value) => typeof(SubInventoryState).GetField(name, PrivateInstance).SetValue(_state, value);

            #endregion
        }

        [TearDown]
        public void TearDown()
        {
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _cts.Dispose();
        }

        [TestCase(InventoryEventType.Update)]
        [TestCase(InventoryEventType.Refresh)]
        public void InitialFailureThenInventoryEventRefetchesAndAppliesModel(InventoryEventType type)
        {
            Fail();
            Assert.IsNull(_state.CurrentSubInventory);
            Receive(type);
            Assert.AreEqual(2, _query.RequestCount);
            var model = Model();
            _query.Complete(model);
            Assert.AreSame(model, _state.CurrentSubInventory);
            Receive(InventoryEventType.Update);
            Assert.AreEqual(7, model.SubInventory[0].Count);
        }

        [Test]
        public void FailureThenRemoveCancelsAndRejectsLaterUpdateOrRefresh()
        {
            Fail();
            Receive(InventoryEventType.Remove);
            Assert.IsTrue(_cts.IsCancellationRequested);
            Assert.IsTrue((bool)typeof(SubInventoryState).GetField("_shouldClose", PrivateInstance).GetValue(_state));
            Receive(InventoryEventType.Update);
            Receive(InventoryEventType.Refresh);
            Assert.AreEqual(1, _query.RequestCount);
            Assert.IsNull(_state.CurrentSubInventory);
        }

        [Test]
        public void FailureThenExitRejectsAllLateInventoryEvents()
        {
            Fail();
            // 受信経路の閉鎖は、通信に依存しないOnExitの解除処理を通す
            // Close the receiver through OnExit's cleanup without a live subscription
            _state.OnExit();
            Receive(InventoryEventType.Update);
            Receive(InventoryEventType.Refresh);
            Receive(InventoryEventType.Remove);
            Assert.AreEqual(1, _query.RequestCount);
            Assert.IsNull(_state.CurrentSubInventory);
        }

        [Test]
        public void RemoveDuringRefetchPreventsTheLateSuccessfulResponseFromApplying()
        {
            Fail();
            Receive(InventoryEventType.Update);
            Receive(InventoryEventType.Remove);
            _query.Complete(Model());
            Assert.IsNull(_state.CurrentSubInventory);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpdateAfterRefreshFailureRefetchesBeforeApplyingItemSlots(bool hadFluid)
        {
            var model = new SubInventoryModel(_identifier);
            if (hadFluid) model.SetFluids(new[] { new FluidMessagePack(1, 20, 100) });
            _query.Complete(model);
            Receive(InventoryEventType.Refresh);
            Fail();
            Receive(InventoryEventType.Update);
            Receive(InventoryEventType.Update);
            Assert.AreSame(model, _state.CurrentSubInventory);
            Assert.AreEqual(0, model.Count);
            Assert.AreEqual(3, _query.RequestCount);
            _query.Complete(Model());
            Assert.AreEqual(4, _query.RequestCount);
            var current = Model();
            current.SetItems(new[] { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() });
            _query.Complete(current);
            Receive(InventoryEventType.Update);
            var secondSlot = UnifiedInventoryEventMessagePack.CreateUpdate(_identifier, 1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 9));
            typeof(SubInventoryState).GetMethod("OnUnifiedInventoryEvent", PrivateInstance).Invoke(_state, new object[] { MessagePackSerializer.Serialize(secondSlot) });
            Assert.AreEqual(7, current.SubInventory[0].Count);
            Assert.AreEqual(9, current.SubInventory[1].Count);
        }

        private void Receive(InventoryEventType type)
        {
            var packet = type switch
            {
                InventoryEventType.Update => UnifiedInventoryEventMessagePack.CreateUpdate(_identifier, 0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 7)),
                InventoryEventType.Remove => UnifiedInventoryEventMessagePack.CreateRemove(_identifier),
                _ => UnifiedInventoryEventMessagePack.CreateRefresh(_identifier),
            };
            typeof(SubInventoryState).GetMethod("OnUnifiedInventoryEvent", PrivateInstance).Invoke(_state, new object[] { MessagePackSerializer.Serialize(packet) });
        }

        private void Fail()
        {
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("controlled request failure"));
            _query.Fail();
        }

        private SubInventoryModel Model()
        {
            var model = new SubInventoryModel(_identifier);
            model.SetItems(new[] { ServerContext.ItemStackFactory.CreatEmpty() });
            return model;
        }

        private sealed class ControlledQuery : ISubInventoryQuery
        {
            private readonly Queue<UniTaskCompletionSource<SubInventoryModel>> _pending = new();
            public int RequestCount { get; private set; }
            public UniTask<SubInventoryModel> Load(ISubInventorySource source, CancellationToken ct)
            {
                RequestCount++;
                var result = new UniTaskCompletionSource<SubInventoryModel>();
                _pending.Enqueue(result);
                return result.Task;
            }
            public void Complete(SubInventoryModel model) => _pending.Dequeue().TrySetResult(model);
            public void Fail() => _pending.Dequeue().TrySetException(new InvalidOperationException("controlled request failure"));
        }
    }
}
