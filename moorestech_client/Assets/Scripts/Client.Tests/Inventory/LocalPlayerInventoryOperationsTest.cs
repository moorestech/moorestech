using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Inventory.Actions;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Inventory
{
    /// <summary>
    /// 本番の持ち物の口がコントローラの移動の結果（拒否と何もしない移動を含む）を移動の結果の列挙へ写すことを確かめる
    /// Verifies the production inventory port maps the controller's move result (denials and no-effect moves included) to the move outcome enum
    /// </summary>
    public class LocalPlayerInventoryOperationsTest
    {
        private const int MainSlot = 5;
        private const int OtherMainSlot = 6;
        private static readonly Guid ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid OtherItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [Test]
        public void 装備の選択が変わるとOnSnapshotChangedが流れる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var equipment = new LocalPlayerEquipment();
            var operations = new LocalPlayerInventoryOperations(new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment), equipment);
            var signals = 0;
            operations.OnSnapshotChanged.Subscribe(_ => signals++);

            // SetSelectedIndex はサーバーへ送るので、確定の入口 ApplySelected で選択を変える
            // SetSelectedIndex sends to the server, so change the selection through the confirmation entry ApplySelected
            equipment.ApplySelected(1);

            Assert.AreEqual(1, signals);
        }

        [Test]
        public void 空の枠から掴むとSourceEmptyになる()
        {
            var operations = Create(out _);

            Assert.AreEqual(PlayerItemMoveOutcome.SourceEmpty, operations.MoveToGrab(PlayerSlotRef.Main(MainSlot), 1));
        }

        [Test]
        public void 枠の個数より多く掴むとSourceCountShortで枠は変わらない()
        {
            var operations = Create(out var controller);
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ItemGuid), 10));

            Assert.AreEqual(PlayerItemMoveOutcome.SourceCountShort, operations.MoveToGrab(PlayerSlotRef.Main(MainSlot), 11));
            Assert.AreEqual(10, controller.LocalPlayerInventory[MainSlot].Count);
        }

        [Test]
        public void 別IDの枠への部分移動はNoEffectと警告で両方の枠は変わらない()
        {
            // コントローラが NoEffect を返すので、成功と区別して開発者ログへ出す
            // The controller returns NoEffect, so it is told apart from success and logged for developers
            var operations = Create(out var controller);
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ItemGuid), 10));
            controller.SetMainItem(OtherMainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(OtherItemGuid), 3));
            LogAssert.Expect(LogType.Warning, new Regex("move has no effect"));

            Assert.AreEqual(PlayerItemMoveOutcome.NoEffect, operations.MoveBetweenSlots(PlayerSlotRef.Main(MainSlot), PlayerSlotRef.Main(OtherMainSlot), 4));
            Assert.AreEqual(10, controller.LocalPlayerInventory[MainSlot].Count);
            Assert.AreEqual(3, controller.LocalPlayerInventory[OtherMainSlot].Count);
        }

        [Test]
        public void 零個の移動はNoEffectと警告になる()
        {
            var operations = Create(out var controller);
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ItemGuid), 10));
            LogAssert.Expect(LogType.Warning, new Regex("move has no effect"));

            Assert.AreEqual(PlayerItemMoveOutcome.NoEffect, operations.MoveToGrab(PlayerSlotRef.Main(MainSlot), 0));
            Assert.AreEqual(10, controller.LocalPlayerInventory[MainSlot].Count);
        }

        [Test]
        public void 同じ枠への移動はNoEffectと警告で枠は変わらない()
        {
            // 加算の後の書き戻しでスタックが減るので、コントローラが何もしない移動として返す
            // The add-then-write-back would shrink the stack, so the controller returns it as a no-effect move
            var operations = Create(out var controller);
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ItemGuid), 10));
            LogAssert.Expect(LogType.Warning, new Regex("move has no effect"));

            Assert.AreEqual(PlayerItemMoveOutcome.NoEffect, operations.MoveBetweenSlots(PlayerSlotRef.Main(MainSlot), PlayerSlotRef.Main(MainSlot), 5));
            Assert.AreEqual(10, controller.LocalPlayerInventory[MainSlot].Count);
        }

        private static LocalPlayerInventoryOperations Create(out LocalPlayerInventoryController controller)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var equipment = new LocalPlayerEquipment();
            controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment);
            return new LocalPlayerInventoryOperations(controller, equipment);
        }
    }
}
