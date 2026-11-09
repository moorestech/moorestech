using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface.Subscription;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// 本番のブロック側の口がブロックの枠を結合インベントリの MainSlotCount + 枠番号へ写すことを確かめる（前例 LocalPlayerInventoryOperationsTest）
    /// Verifies the production block-side port maps a block slot to MainSlotCount + slot in the combined inventory (precedent LocalPlayerInventoryOperationsTest)
    /// </summary>
    public class LocalBlockInventoryOperationsTest
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid StoneGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [SetUp]
        public void SetUp()
        {
            // 引数の在庫をマスタから作るので、テスト本体より先にコンテナを組む
            // Build the container before the test body, since the argument stacks are created from the master
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // サーバーへ送る移動（Moved）は ClientContext が無いので起こさない。番地が外れると結果が変わる拒否・何もしないで確かめる
        // Never trigger a sent move (Moved) since ClientContext is absent; assert via refusals and no-effect moves whose result changes when the address is wrong

        [Test]
        public void ブロックの枠1から掴むとサブの枠1を読む()
        {
            // サブの枠 1 は 3 個なので 4 個は足りない。メインの枠 1（空）を読めば SourceEmpty になる
            // Sub slot 1 holds 3, so 4 is short; reading main slot 1 (empty) would give SourceEmpty
            var operations = Create(out var controller, Item(WoodGuid, 10), Item(WoodGuid, 3));

            Assert.AreEqual(PlayerItemMoveOutcome.SourceCountShort, operations.Move(ItemMoveSlotRef.Block(1), ItemMoveSlotRef.Grab, 4));
            Assert.AreEqual(3, controller.LocalPlayerInventory[controller.LocalPlayerInventory.MainSlotCount + 1].Count);
        }

        [Test]
        public void ブロックの枠0はメインの枠0と取り違えない()
        {
            var operations = Create(out var controller, ServerContext.ItemStackFactory.CreatEmpty());
            controller.SetMainItem(0, Item(WoodGuid, 10));

            Assert.AreEqual(PlayerItemMoveOutcome.SourceEmpty, operations.Move(ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 1));
            Assert.AreEqual(10, controller.LocalPlayerInventory[0].Count);
        }

        [Test]
        public void メインからブロックの枠1への移動は宛先もサブの枠1で決まる()
        {
            // 宛先のサブの枠 1 は別 ID なので部分移動は何もしない。メインの枠 1（空）へ写すと Moved になり送信へ進む
            // Sub slot 1 holds another ID, so a partial move does nothing; mapping to main slot 1 (empty) would be Moved and go to sending
            var operations = Create(out var controller, ServerContext.ItemStackFactory.CreatEmpty(), Item(StoneGuid, 3));
            controller.SetMainItem(0, Item(WoodGuid, 10));
            LogAssert.Expect(LogType.Warning, new Regex("move has no effect"));

            Assert.AreEqual(PlayerItemMoveOutcome.NoEffect, operations.Move(ItemMoveSlotRef.Main(0), ItemMoveSlotRef.Block(1), 4));
            Assert.AreEqual(10, controller.LocalPlayerInventory[0].Count);
        }

        private static IItemStack Item(Guid guid, int count)
        {
            return ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(guid), count);
        }

        private static LocalBlockInventoryOperations Create(out LocalPlayerInventoryController controller, params IItemStack[] subSlots)
        {
            var subInventory = new FixedSubInventory();
            subInventory.SubInventory.AddRange(subSlots);
            controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
            controller.SetSubInventory(subInventory);
            var reader = new BlockSlotReader(new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed)), new FixedRawSubInventorySlots(Array.Empty<SlotItem>()));
            return new LocalBlockInventoryOperations(controller, reader);
        }

        // 番地の確認に要る枠だけを持つサブインベントリ
        // Sub-inventory holding only the slots the address checks need
        private sealed class FixedSubInventory : ISubInventory
        {
            public List<IItemStack> SubInventory { get; } = new();
            public int Count => SubInventory.Count;
            public ISubInventoryIdentifier ISubInventoryIdentifier => null;
        }
    }
}
