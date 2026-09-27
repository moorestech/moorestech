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
    public class VeinMiningCapacityAndCooldownTest : VeinMiningProtocolTestBase
    {

        [Test]
        public void インベントリに空きが無いとき採掘は成立せずクールダウンも消費しない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();
            EquipTool(playerInventory, ToolItemGuid);
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // 別アイテムで満載にし、鉱石の受け皿を無くす
            // Fill every slot with another item so the ore has nowhere to land
            var fillerItemId = MasterHolder.ItemMaster.GetItemId(UnmatchedToolItemGuid);
            var mainInventory = playerInventory.MainOpenableInventory;
            for (var slot = 0; slot < mainInventory.GetSlotSize(); slot++)
            {
                mainInventory.SetItem(slot, ServerContext.ItemStackFactory.Create(fillerItemId, 1));
            }

            // 受け取れない取得物を消滅させず、打撃自体を拒否する
            // Refuse the swing itself instead of letting undeliverable drops vanish
            Assert.AreEqual(VeinMiningResult.InventoryFull, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, mainInventory, out _));

            // 拒否時はクールダウンを消費しないので、空けた直後に掘れる
            // A refusal consumes no cooldown, so mining succeeds immediately after freeing a slot
            mainInventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, mainInventory, out _));
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
            Assert.AreEqual(MiningAttackResult.Success, mapObjectMiningService.TryAttack(PlayerId, mapObject, equipped, playerInventory.MainOpenableInventory, out _));
            Assert.AreEqual(VeinMiningResult.CooldownNotElapsed, veinService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, playerInventory.MainOpenableInventory, out _));

            // 経過後は再採掘可能
            // Mine again after elapsed time
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(ExpectedAttackSpeed) + 1);
            Assert.AreEqual(VeinMiningResult.Success, veinService.TryMine(PlayerId, IronVeinGuid, InsideIronVein, equipped, playerInventory.MainOpenableInventory, out _));
        }

        [Test]
        public void プロトコル経由でveinを採掘すると対応する鉱石がインベントリに入る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            EquipTool(playerInventory, ToolItemGuid);

            // 座標から報酬を解決
            // Resolve reward from position
            var request = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(IronVeinGuid, InsideIronVein);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.BoundPacketContext.Bind(PlayerId));

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
    }
}
