using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Server.Protocol.PacketResponse.InventoryItemMoveProtocol;

namespace Tests.CombinedTest.Game.InventoryMove
{
    // 機械のレシピ束縛スロットへの移動拒否（サービスの結果とプロトコルのログ・通知）を検証する
    // Verifies move rejections against a machine's recipe-bound slots (service result, protocol log and notification)
    public class InventoryItemMoveRejectionTest
    {
        private const int PlayerId = 1;

        [Test]
        public void FullSwapAgainstBoundMachineSlotIsRejectedWithoutWriting()
        {
            var setup = SetupBoundMachineWithPlayer();
            var boundCount = setup.Recipe.InputItems[0].Count;
            setup.BlockInventory.InsertItem(ServerContext.ItemStackFactory.Create(setup.BoundItemId, boundCount));
            Assert.AreEqual(setup.BoundItemId, setup.BlockInventory.GetItem(0).Id);
            setup.PlayerInventory.SetItem(0, ServerContext.ItemStackFactory.Create(setup.UnboundItemId, boundCount));

            var result = InventoryItemMoveService.Move(setup.PlayerInventory, 0, setup.BlockInventory, 0, boundCount);

            Assert.AreEqual(InventoryItemMoveResult.RejectedByDestination, result);
            Assert.AreEqual(setup.BoundItemId, setup.BlockInventory.GetItem(0).Id, "束縛外swapで機械側の中身が消失/変化してはならない");
            Assert.AreEqual(boundCount, setup.BlockInventory.GetItem(0).Count);
            Assert.AreEqual(setup.UnboundItemId, setup.PlayerInventory.GetItem(0).Id, "束縛外swapでプレイヤー側のアイテムが複製/消失してはならない");
            Assert.AreEqual(boundCount, setup.PlayerInventory.GetItem(0).Count);
        }

        [Test]
        public void PartialSwapOfDifferentItemsIsRejectedWithoutWriting()
        {
            var setup = SetupBoundMachineWithPlayer();
            setup.BlockInventory.InsertItem(ServerContext.ItemStackFactory.Create(setup.BoundItemId, 1));
            setup.PlayerInventory.SetItem(0, ServerContext.ItemStackFactory.Create(setup.UnboundItemId, 3));

            var result = InventoryItemMoveService.Move(setup.PlayerInventory, 0, setup.BlockInventory, 0, 1);

            Assert.AreEqual(InventoryItemMoveResult.RejectedPartialSwap, result);
            Assert.AreEqual(ServerContext.ItemStackFactory.Create(setup.BoundItemId, 1), setup.BlockInventory.GetItem(0));
            Assert.AreEqual(ServerContext.ItemStackFactory.Create(setup.UnboundItemId, 3), setup.PlayerInventory.GetItem(0));
        }

        [Test]
        public void ProtocolLogsAndNotifiesRejectedPlacementIntoEmptyBoundMachineSlot()
        {
            var setup = SetupBoundMachineWithPlayer();
            var sink = EventTestUtil.RegisterCaptureSink(setup.ServiceProvider, PlayerId);
            setup.PlayerInventory.SetItem(0, ServerContext.ItemStackFactory.Create(setup.UnboundItemId, 3));

            // 拒否ログは座標・プレイヤー・両側のアイテムと束縛の拒否理由まで実値で出る
            // The rejection log carries the actual position, player, both stacks, and the binding's rejection reason
            var expectedLog = $"[InventoryItemMoveProtocol] Move rejected: result=RejectedByDestination player={PlayerId} from=Main(player={PlayerId})[0] fromItemId={setup.UnboundItemId} fromCount=3 fromReason=Allowed to=Block{new Vector3IntMessagePack(Vector3Int.one)}[0] toItemId={ItemMaster.EmptyItemId} toCount=0 toReason={MachineSlotPlacementCheck.ItemNotBoundToSlot} requestedCount=3";
            LogAssert.Expect(LogType.Warning, new Regex($"^{Regex.Escape(expectedLog)}$"));
            SendMove(setup.Packet, 3, InventoryIdentifierMessagePack.CreateMainMessage(PlayerId), InventoryIdentifierMessagePack.CreateBlockMessage(Vector3Int.one));

            Assert.AreEqual(0, setup.BlockInventory.GetItem(0).Count, "束縛外アイテムは機械の入力スロットへ入らない");
            Assert.AreEqual(ServerContext.ItemStackFactory.Create(setup.UnboundItemId, 3), setup.PlayerInventory.GetItem(0), "拒否されたアイテムはプレイヤー側に残る");
            Assert.AreEqual(new[] { "denied.inventoryMoveSlotRejected" }, TakeDeniedMessageIds(sink));
        }

        [Test]
        public void ProtocolNotifiesPartialSwapOfDifferentItems()
        {
            var setup = SetupBoundMachineWithPlayer();
            var sink = EventTestUtil.RegisterCaptureSink(setup.ServiceProvider, PlayerId);
            setup.BlockInventory.InsertItem(ServerContext.ItemStackFactory.Create(setup.BoundItemId, 1));
            setup.PlayerInventory.SetItem(0, ServerContext.ItemStackFactory.Create(setup.UnboundItemId, 3));

            LogAssert.Expect(LogType.Warning, new Regex($"^{Regex.Escape("[InventoryItemMoveProtocol] Move rejected: result=RejectedPartialSwap ")}"));
            SendMove(setup.Packet, 1, InventoryIdentifierMessagePack.CreateMainMessage(PlayerId), InventoryIdentifierMessagePack.CreateBlockMessage(Vector3Int.one));

            Assert.AreEqual(new[] { "denied.inventoryMovePartialSwap" }, TakeDeniedMessageIds(sink));
        }

        #region Setup

        private readonly struct BoundMachineSetup
        {
            public readonly PacketResponseCreator Packet;
            public readonly ServiceProvider ServiceProvider;
            public readonly VanillaMachineBlockInventoryComponent BlockInventory;
            public readonly IOpenableInventory PlayerInventory;
            public readonly MachineRecipeMasterElement Recipe;
            public readonly ItemId BoundItemId;
            public readonly ItemId UnboundItemId;

            public BoundMachineSetup(PacketResponseCreator packet, ServiceProvider serviceProvider, VanillaMachineBlockInventoryComponent blockInventory, IOpenableInventory playerInventory, MachineRecipeMasterElement recipe, ItemId boundItemId, ItemId unboundItemId)
            {
                Packet = packet;
                ServiceProvider = serviceProvider;
                BlockInventory = blockInventory;
                PlayerInventory = playerInventory;
                Recipe = recipe;
                BoundItemId = boundItemId;
                UnboundItemId = unboundItemId;
            }
        }

        // Vector3Int.oneにレシピ選択済みの機械を置く。入力/出力が別アイテムである前提も検査する
        // Place a recipe-selected machine at Vector3Int.one; also asserts input and output items differ
        private static BoundMachineSetup SetupBoundMachineWithPlayer()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.InputItems.Length && 0 < r.OutputItems.Length);
            var boundItemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var unboundItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.AreNotEqual(boundItemId, unboundItemId, "テスト前提: 入力素材と出力生産物は別アイテムであること");

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            return new BoundMachineSetup(packet, serviceProvider, blockInventory, playerInventory, recipe, boundItemId, unboundItemId);
        }

        private static void SendMove(PacketResponseCreator packet, int count, InventoryIdentifierMessagePack from, InventoryIdentifierMessagePack to)
        {
            var context = new PacketResponseContext(null);
            context.TryBindPlayerId(PlayerId);
            var payload = MessagePackSerializer.Serialize(new InventoryItemMoveProtocolMessagePack(count, ItemMoveType.SwapSlot, from, 0, to, 0));
            packet.GetPacketResponse(payload, context);
        }

        private static List<string> TakeDeniedMessageIds(CapturedEventSink sink)
        {
            return sink.TakeAll()
                .Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(n => n.Category == NotificationCategory.OperationDenied)
                .Select(n => n.MessageId)
                .ToList();
        }

        #endregion
    }
}
