using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.Inventory
{
    /// <summary>
    /// grab を書き換える全経路で掴み専用の流れが新しい grab とともに流れ、既存の OnInventoryRefreshed は増えないことを確かめる
    /// Verifies the grab-only stream fires with the new grab on every grab-writing path while OnInventoryRefreshed does not increase
    /// </summary>
    public class LocalPlayerInventoryControllerGrabNotifyTest
    {
        private const int MainSlot = 5;
        private const int OtherMainSlot = 6;
        private static readonly Guid ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void スロットからgrabへ移すと流れた時点で新しいgrabが読める()
        {
            var controller = CreateController();
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(), 10));
            var grabCounts = SubscribeGrab(controller);

            // 送信を切ってローカル反映だけを見る
            // Sending is disabled so only the local application is observed
            controller.MoveItem(LocalMoveInventoryType.MainOrSub, MainSlot, LocalMoveInventoryType.Grab, 0, 10, false);

            Assert.AreEqual(new[] { 10 }, grabCounts.ToArray());
        }

        [Test]
        public void grabからスロットへ置いても流れる()
        {
            var controller = CreateController();
            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(), 4));
            var grabCounts = SubscribeGrab(controller);

            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, MainSlot, 1, false);

            Assert.AreEqual(new[] { 3 }, grabCounts.ToArray());
        }

        [Test]
        public void サーバーからのgrabの更新でも流れる()
        {
            // MoveItem 以外の経路（ネットワークのイベントが呼ぶ SetGrabItem）
            // A path other than MoveItem (SetGrabItem, called by the network event)
            var controller = CreateController();
            var grabCounts = SubscribeGrab(controller);

            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(), 7));

            Assert.AreEqual(new[] { 7 }, grabCounts.ToArray());
        }

        [Test]
        public void grabが絡まない移動と弾かれた移動では流れない()
        {
            var controller = CreateController();
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(), 10));
            var grabCounts = SubscribeGrab(controller);

            controller.MoveItem(LocalMoveInventoryType.MainOrSub, MainSlot, LocalMoveInventoryType.MainOrSub, OtherMainSlot, 10, false);
            // 空の grab から移す移動は MoveItem の入口で弾かれ、何も変わらない
            // A move from an empty grab is rejected at MoveItem's entrance and nothing changes
            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, MainSlot, 1, false);

            Assert.IsEmpty(grabCounts);
        }

        [Test]
        public void grabが絡む移動で既存のOnInventoryRefreshedは増えない()
        {
            // 既存の購読者（研究の取り直し・Web の配信）へ要求を増やさない（死活表・D-4）
            // Adds no requests to existing subscribers (research refetch, Web publish); see the liveness table and D-4
            var controller = CreateController();
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(), 10));
            var refreshed = 0;
            controller.OnInventoryRefreshed.Subscribe(_ => refreshed++);

            controller.MoveItem(LocalMoveInventoryType.MainOrSub, MainSlot, LocalMoveInventoryType.Grab, 0, 10, false);
            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, OtherMainSlot, 10, false);

            Assert.AreEqual(0, refreshed);
        }

        private static List<int> SubscribeGrab(LocalPlayerInventoryController controller)
        {
            var counts = new List<int>();
            controller.OnGrabChanged.Subscribe(_ => counts.Add(controller.GrabInventory.Count));
            return counts;
        }

        private static LocalPlayerInventoryController CreateController()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
        }

        private static ItemId ItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(ItemGuid);
        }
    }
}
