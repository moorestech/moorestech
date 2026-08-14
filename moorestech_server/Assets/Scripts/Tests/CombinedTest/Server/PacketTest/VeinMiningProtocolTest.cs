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
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     vein採掘権威を検証
    ///     Verify vein mining authority
    /// </summary>
    public class VeinMiningProtocolTest
    {
        private const int PlayerId = 0;

        // IronVein内の対象座標
        // Target position inside IronVein
        private static readonly Vector3Int InsideIronVein = new(0, 5, 0);
        private static readonly Vector3Int OutsideAnyVein = new(500, 500, 500);
        private static readonly Vector3Int InsideFluidVein = new(5, 0, 0);
        private static readonly Vector3Int InsideNoneItemVein = new(20, 5, 0);
        private static readonly Guid IronVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        private static readonly Guid FluidVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000002");
        private static readonly Guid NoneItemVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000004");
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
            Assert.AreEqual(VeinMiningResult.NoTool, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, out _));

            // 非対応ツールはToolMismatch
            // A non-matching tool yields ToolMismatch
            EquipTool(playerInventory, UnmatchedToolItemGuid);
            Assert.AreEqual(VeinMiningResult.ToolMismatch, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), out _));

            // 設定範囲の個数を取得
            // Get count from configured range
            EquipTool(playerInventory, ToolItemGuid);
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), out var earnedItems));
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
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, IronVeinGuid, OutsideAnyVein, equipped, out _));

            // fluidは採掘対象外
            // Fluid is not minable
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, FluidVeinGuid, InsideFluidVein, equipped, out _));

            // noneは採掘不可
            // None is not minable
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, NoneItemVeinGuid, InsideNoneItemVein, equipped, out _));
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
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, NoneItemVeinGuid, InsideIronVein, equipped, out _));
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, out _));
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

            // 共有クールダウンを検証
            // Verify shared cooldown
            Assert.AreEqual(MiningAttackResult.Success, mapObjectMiningService.TryAttack(PlayerId, mapObject, equipped, out _));
            Assert.AreEqual(VeinMiningResult.CooldownNotElapsed, veinService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, out _));

            // 経過後は再採掘可能
            // Mine again after elapsed time
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(ExpectedAttackSpeed) + 1);
            Assert.AreEqual(VeinMiningResult.Success, veinService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, out _));
        }

        [Test]
        public void プロトコル経由でveinを採掘すると対応する鉱石がインベントリに入る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            EquipTool(playerInventory, ToolItemGuid);

            // 座標から報酬を解決
            // Resolve reward from position
            var request = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(PlayerId, IronVeinGuid, InsideIronVein);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null));

            var expectedItemId = MasterHolder.ItemMaster.GetItemId(((ItemVeinParam)MasterHolder.MapVeinMaster.GetElementOrNull(IronVeinGuid).VeinParam).ItemGuid);
            Assert.AreEqual(1, CountMainInventoryItem(playerInventory, expectedItemId));

            #region Internal

            int CountMainInventoryItem(PlayerInventoryData inventory, ItemId itemId)
            {
                var mainInventory = inventory.MainOpenableInventory;
                return Enumerable.Range(0, mainInventory.GetSlotSize()).
                    Where(slot => mainInventory.GetItem(slot).Id == itemId).
                    Sum(slot => mainInventory.GetItem(slot).Count);
            }

            #endregion
        }

        private void EquipTool(PlayerInventoryData playerInventory, Guid toolItemGuid)
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(toolItemGuid);
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }
    }
}
