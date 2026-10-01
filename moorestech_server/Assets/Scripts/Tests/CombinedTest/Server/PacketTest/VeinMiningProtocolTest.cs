using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MapModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     vein採掘権威を検証
    ///     Verify vein mining authority
    /// </summary>
    public class VeinMiningProtocolTest : VeinMiningProtocolTestBase
    {

        [Test]
        public void 対応ツール装備時のみvein上の座標で鉱石が1振りごとに入る()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();

            // マスタの初期装備が既にツールなので、素手の検証は装備を外してから行う
            // The master's initial equipment is already a tool, so unequip before checking the bare-hands case
            playerInventory.EquipmentInventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // 素手はNoTool
            // Bare hands yield NoTool
            Assert.AreEqual(VeinMiningResult.NoTool, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, playerInventory.MainOpenableInventory, out _));

            // 非対応ツールはToolMismatch
            // A non-matching tool yields ToolMismatch
            EquipTool(playerInventory, UnmatchedToolItemGuid);
            Assert.AreEqual(VeinMiningResult.ToolMismatch, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), playerInventory.MainOpenableInventory, out _));

            // 設定範囲の個数を取得
            // Get count from configured range
            EquipTool(playerInventory, ToolItemGuid);
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), playerInventory.MainOpenableInventory, out var earnedItems));
            Assert.AreEqual(1, earnedItems.Sum(item => item.Count));
            var ironVein = MasterHolder.MapVeinMaster.GetElementOrNull(IronVeinGuid);
            var veinItemGuid = ((ItemVeinParam)ironVein.VeinParam).ItemGuid;
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(veinItemGuid), earnedItems[0].Id);
        }

        // 取得個数の抽選が世界共有の乱数でないと、同じスナップショットとパケット列を再生しても取得数がずれる
        // If the drop-count roll does not come from the shared world random, replaying the same snapshot and packets yields a different count
        [Test]
        public void vein手掘りの取得個数抽選はGameRandomを1回だけ引く()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();
            EquipTool(playerInventory, ToolItemGuid);
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            var drawnOnce = GameRandomDrawAssert.StateAfterDraws(1);
            GameRandomDrawAssert.BeginDrawCount();
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, playerInventory.MainOpenableInventory, out _));
            GameRandomDrawAssert.AssertDrawn(drawnOnce, "vein手掘りの取得個数抽選が世界共有の乱数を引いていない");

            // 掘れなかった経路は取得物を作らないので、1回も引いてはならない
            // A refused swing creates no drops, so it must not draw at all
            var untouched = GameRandomDrawAssert.StateAfterDraws(0);
            GameRandomDrawAssert.BeginDrawCount();
            Assert.AreEqual(VeinMiningResult.VeinNotFound, miningService.TryMine(PlayerId, IronVeinGuid, OutsideAnyVein, equipped, playerInventory.MainOpenableInventory, out _));
            GameRandomDrawAssert.AssertDrawn(untouched, "掘れなかった経路が乱数を引いている");
        }

        [Test]
        public void vein外とfluid_veinとnone設定のitem_veinでは掘れない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();
            EquipTool(playerInventory, ToolItemGuid);
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // vein AABBの外は掘れない
            // Positions outside every vein AABB are not minable
            Assert.AreEqual(VeinMiningResult.VeinNotFound, miningService.TryMine(PlayerId, IronVeinGuid, OutsideAnyVein, equipped, playerInventory.MainOpenableInventory, out _));

            // fluid鉱脈はitem鉱脈の索引に載らないため、座標上に手掘り対象が存在しない扱いになる
            // A fluid vein is absent from the item-vein index, so no hand-mining target exists at that position
            Assert.AreEqual(VeinMiningResult.VeinNotFound, miningService.TryMine(PlayerId, FluidVeinGuid, InsideFluidVein, equipped, playerInventory.MainOpenableInventory, out _));

            // noneは採掘不可
            // None is not minable
            Assert.AreEqual(VeinMiningResult.HandMiningNotAllowed, miningService.TryMine(PlayerId, NoneItemVeinGuid, InsideNoneItemVein, equipped, playerInventory.MainOpenableInventory, out _));
        }

        [Test]
        public void 掘れる座標でも狙ったvein以外のguidでは掘れない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();
            EquipTool(playerInventory, ToolItemGuid);
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // guidが別なので拒否される
            // Another vein's guid is rejected
            Assert.AreEqual(VeinMiningResult.VeinGuidMismatch, miningService.TryMine(PlayerId, NoneItemVeinGuid, InsideIronVein, equipped, playerInventory.MainOpenableInventory, out _));
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, playerInventory.MainOpenableInventory, out _));
        }
    }
}
