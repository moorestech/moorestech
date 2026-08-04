using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MapModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     vein手掘りの権威判定（座標→vein解決・ツール照合・1振り1ドロップ・クールダウン共有）を検証する
    ///     Verifies vein hand-mining authority: position→vein resolution, tool matching, per-swing drops, shared cooldown
    /// </summary>
    public class VeinMiningProtocolTest
    {
        private const int PlayerId = 0;

        // ForUnitTestマスタのIronVein(minable, tool=1234-0001, attackSpeed0.2)と対応座標
        // ForUnitTest master's IronVein (minable, tool 1234-0001, attackSpeed 0.2) and a position inside it
        private static readonly Vector3Int InsideIronVein = new(0, 5, 0);
        private static readonly Vector3Int OutsideAnyVein = new(500, 500, 500);
        private static readonly Vector3Int InsideFluidVein = new(5, 0, 0);
        private static readonly Vector3Int InsideNoneItemVein = new(20, 5, 0);
        private static readonly Guid IronVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid UnmatchedToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private static readonly Guid MiningMapObjectGuid = Guid.Parse("00000000-0000-2222-0000-000000000001");
        private const double ExpectedAttackSpeed = 0.2;

        [Test]
        public void 対応ツール装備時のみvein上の座標で鉱石が1振りごとに入る()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // 素手はNoTool
            // Bare hands yield NoTool
            Assert.AreEqual(VeinMiningResult.NoTool, miningService.TryMine(PlayerId, InsideIronVein, equipped, out _));

            // 非対応ツールはToolMismatch
            // A non-matching tool yields ToolMismatch
            EquipTool(playerInventory, UnmatchedToolItemGuid);
            Assert.AreEqual(VeinMiningResult.ToolMismatch, miningService.TryMine(PlayerId, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), out _));

            // 対応ツールでminCount〜maxCount（テストマスタは1〜1固定）個ドロップする
            // The matching tool drops minCount..maxCount items (fixed 1..1 in the test master)
            EquipTool(playerInventory, ToolItemGuid);
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), out var earnedItems));
            Assert.AreEqual(1, earnedItems.Sum(item => item.Count));
            var ironVein = MasterHolder.MapVeinMaster.GetElementOrNull(IronVeinGuid);
            var veinItemGuid = ((ItemVeinParam)ironVein.VeinParam).ItemGuid;
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(veinItemGuid), earnedItems[0].Id);
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
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, OutsideAnyVein, equipped, out _));

            // fluid veinはItemMapVeinDatastoreの対象外なので同じくNoMinableVein
            // Fluid veins are outside ItemMapVeinDatastore, so also NoMinableVein
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, InsideFluidVein, equipped, out _));

            // none設定のitem veinはDatastoreには存在するがminable判定で弾かれる
            // A none-configured item vein exists in the datastore but is rejected by the minable check
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, InsideNoneItemVein, equipped, out _));
        }

        [Test]
        public void mapObject採掘とクールダウンを共有する()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var veinService = serviceProvider.GetService<VeinHandMiningService>();
            var mapObjectMiningService = serviceProvider.GetService<MapObjectMiningService>();
            var mapObject = ServerContext.MapObjectDatastore.MapObjects.First(mapObject => mapObject.MapObjectGuid == MiningMapObjectGuid);
            EquipTool(playerInventory, ToolItemGuid);
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // mapObjectへの1振り直後にveinを掘ると共有クールダウンで弾かれる
            // Mining a vein immediately after a mapObject swing is rejected by the shared cooldown
            Assert.AreEqual(MiningAttackResult.Success, mapObjectMiningService.TryAttack(PlayerId, mapObject, equipped, out _));
            Assert.AreEqual(VeinMiningResult.CooldownNotElapsed, veinService.TryMine(PlayerId, InsideIronVein, equipped, out _));

            // attackSpeed分のtickを進めればvein採掘は再び通る
            // After advancing attackSpeed worth of ticks, vein mining succeeds again
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(ExpectedAttackSpeed) + 1);
            Assert.AreEqual(VeinMiningResult.Success, veinService.TryMine(PlayerId, InsideIronVein, equipped, out _));
        }

        private void EquipTool(PlayerInventoryData playerInventory, Guid toolItemGuid)
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(toolItemGuid);
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }
    }
}
