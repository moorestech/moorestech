using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Interface.Component;
using Game.Context;
using Game.UnlockState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.OrbitalCargo;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.OrbitalCargo
{
    // 選択部品だけを偽の入れ口と組んで検証する
    // Verify the selector alone, wired to a fake inlet
    public class OrbitalCargoSelectorComponentTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 運べる解放済みの種類だけを選び同じ種類の選び直しは何もしない()
        {
            var inlet = new FakeInletSlots();
            var state = new OrbitalCargoSelectionState();
            var selector = new OrbitalCargoSelectorComponent(state, inlet, ServerContext.GetService<IGameUnlockStateDataController>());
            var notifications = 0;
            using var subscription = state.OnChanged.Subscribe(_ => notifications++);
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(selector.Selection);

            // 運べない種類は拒否し、運べる種類は選んで返却を1回呼ぶ
            // Refuse a non-carried kind; select a carried kind and call the refund once
            LogAssert.Expect(LogType.Warning, new Regex("積荷の選択を拒否しました 理由:NotCarriedHere"));
            Assert.AreEqual(OrbitalCargoSelectionResult.NotCarriedHere, selector.SelectCargo(ForUnitTestItemId.ItemId1, OrbitalCargoTestUtil.NoRefundInventory()));
            Assert.AreEqual(OrbitalCargoSelectionResult.Success, selector.SelectCargo(ForUnitTestItemId.TestElevatorSatellite, OrbitalCargoTestUtil.NoRefundInventory()));
            Assert.AreEqual(ForUnitTestItemId.TestElevatorSatellite, ((SelectedOrbitalCargo)selector.Selection).PayloadItemId);
            Assert.AreEqual(1, notifications);
            CollectionAssert.AreEqual(new[] { ForUnitTestItemId.TestElevatorSatellite }, inlet.RefundedFor);

            // 同じ種類の選び直しは返却も通知もしない
            // Reselecting the same kind neither refunds nor notifies
            Assert.AreEqual(OrbitalCargoSelectionResult.Success, selector.SelectCargo(ForUnitTestItemId.TestElevatorSatellite, OrbitalCargoTestUtil.NoRefundInventory()));
            Assert.AreEqual(1, inlet.RefundedFor.Count);
            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void 未解放の種類は拒否し選択を変えない()
        {
            var selector = Create(new FakeInletSlots());
            OrbitalCargoTestUtil.LockItem(ForUnitTestItemId.TestElevatorSatellite);
            LogAssert.Expect(LogType.Warning, new Regex("理由:Locked"));
            Assert.AreEqual(OrbitalCargoSelectionResult.Locked, selector.SelectCargo(ForUnitTestItemId.TestElevatorSatellite, OrbitalCargoTestUtil.NoRefundInventory()));
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(selector.Selection);
        }

        [Test]
        public void セーブ往復は選択を保ちロードは解放を見ない()
        {
            var inlet = new FakeInletSlots();
            var selector = Create(inlet);
            selector.SelectCargo(ForUnitTestItemId.TestElevatorSatellite, OrbitalCargoTestUtil.NoRefundInventory());
            var states = new Dictionary<string, object> { { selector.SaveKey, JObject.FromObject(selector.GetSaveState()) } };
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.TestElevatorSatellite).ToString(), ((JObject)states[selector.SaveKey])["selectedCargoItemGuid"].Value<string>());

            // ロック後でも保存した選択を復元する（ADR 0106 3）
            // Restore the saved selection even after locking (ADR 0106 3)
            OrbitalCargoTestUtil.LockItem(ForUnitTestItemId.TestElevatorSatellite);
            var loaded = Load(states, inlet);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorSatellite, ((SelectedOrbitalCargo)loaded.Selection).PayloadItemId);
        }

        [Test]
        public void 未選択で保存した台は警告なしで未選択に戻る()
        {
            var inlet = new FakeInletSlots();
            var states = new Dictionary<string, object> { { OrbitalCargoSelectorComponent.SaveKeyName, JObject.FromObject(Create(inlet).GetSaveState()) } };
            var loaded = Load(states, inlet);
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(loaded.Selection);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 項目の無い旧セーブは欠損ログを一件だけ出し未選択で読み込む()
        {
            var messages = new List<string>();
            var inlet = new FakeInletSlots();
            Application.logMessageReceived += Record;
            try
            {
                Assert.IsInstanceOf<UnselectedOrbitalCargo>(Load(new Dictionary<string, object>(), inlet).Selection);
            }
            finally
            {
                Application.logMessageReceived -= Record;
            }
            CollectionAssert.AreEqual(new[] { $"Log: ブロックのセーブ状態にキー {OrbitalCargoSelectorComponent.SaveKeyName} がないため、このコンポーネントの復元を見送ります" }, messages);

            #region Internal

            void Record(string message, string stackTrace, LogType type) => messages.Add($"{type}: {message}");

            #endregion
        }

        [TestCase("unknown", "この台が運ぶ積荷ではない")]
        [TestCase("notCarried", "この台が運ぶ積荷ではない")]
        public void 読めない選択は警告して未選択で読み込む(string scenario, string message)
        {
            var inlet = new FakeInletSlots();
            var states = new Dictionary<string, object>();
            var guid = scenario == "unknown" ? Guid.NewGuid() : MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.ItemId1);
            states[OrbitalCargoSelectorComponent.SaveKeyName] = JObject.FromObject(new OrbitalCargoSelectionSaveJsonObject { SelectedCargoItemGuid = guid.ToString() });
            LogAssert.Expect(LogType.Warning, new Regex(message));
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(Load(states, inlet).Selection);
        }

        #region Helpers

        private static OrbitalCargoSelectorComponent Create(FakeInletSlots inlet)
        {
            return new OrbitalCargoSelectorComponent(new OrbitalCargoSelectionState(), inlet, ServerContext.GetService<IGameUnlockStateDataController>());
        }

        private static OrbitalCargoSelectorComponent Load(Dictionary<string, object> states, FakeInletSlots inlet)
        {
            return new OrbitalCargoSelectorComponent(states, new OrbitalCargoSelectionState(), inlet, ServerContext.GetService<IGameUnlockStateDataController>());
        }

        // 衛星と大口の積荷だけを運ぶ偽の入れ口。返却の呼び出しだけを記録する
        // A fake inlet carrying the satellite and bulk payloads; it only records refund calls
        private class FakeInletSlots : IOrbitalCargoInletSlots
        {
            public IReadOnlyCollection<ItemId> CarriedPayloadItemIds { get; } = new[] { ForUnitTestItemId.TestElevatorSatellite, ForUnitTestItemId.TestElevatorBulkPayload };
            public readonly List<ItemId> RefundedFor = new();
            public void RefundUnselectedPayloads(SelectedOrbitalCargo selection, IOpenableInventory refundInventory) => RefundedFor.Add(selection.PayloadItemId);
        }

        #endregion
    }
}
