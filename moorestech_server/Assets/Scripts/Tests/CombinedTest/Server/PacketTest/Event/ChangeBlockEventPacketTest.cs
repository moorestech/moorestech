using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.EventProtocol;
using System;
using System.Linq;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ChangeBlockEventPacketTest
    {
        [Test]
        public void MachineChangeStateEvent()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            Vector3Int pos = new(0, 0);
            
            //機械のブロックを作る
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            // レシピを明示選択してから材料を投入する
            // Explicitly select the recipe before inserting materials
            var machineGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machineGuid);
            MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);

            //機械ブロックにアイテムを挿入するのでそのアイテムを挿入する
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var item1 = itemStackFactory.Create(new ItemId(1), 3);
            var item2 = itemStackFactory.Create(new ItemId(2), 1);
            
            var blockInventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            blockInventory.InsertItem(item1);
            blockInventory.InsertItem(item2);
            
            
            //稼働用の電気を無限発電機からワイヤー経由で供給する
            //Supply operating power from an infinite generator through a wire
            Vector3Int generatorPos = new(0, 0, 2);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, generatorPos);

            //電力tickは機械Updateより先に確定するため、プリロード不要で同tick内に満充電が観測できる
            //The electric tick settles before machine updates, so no preload is needed to observe a full charge within the tick

            //最初にイベントをリクエストして、ブロードキャストを受け取れるようにする
            packetResponse.GetPacketResponseForTest(EventTestUtil.EventRequestData(0), new PacketResponseContext());

            //電力がセグメント経由で機械へ行き渡り稼働状態になるまで数tick進める
            //Advance several ticks until power propagates through the segment and the machine starts processing
            for (var i = 0; i < 3; i++) GameUpdater.UpdateOneTick();
            
            
            //ステートが実行中になっているかをチェック
            List<byte[]> response = packetResponse.GetPacketResponseForTest(EventTestUtil.EventRequestData(0), new PacketResponseContext());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            // アイドル給電イベントも並ぶため、idle→processingの遷移イベントを明示的に選択する
            // Idle supply events are also queued, so pick the idle-to-processing transition event explicitly
            var expectedTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(machine.BlockPositionInfo);
            BlockStateMessagePack transitionStateData = null;
            BlockStateMessagePack latestStateData = null;
            foreach (var eventMessage in eventMessagePack.Events)
            {
                if (eventMessage.Tag != expectedTag) continue;
                var stateData = MessagePackSerializer.Deserialize<BlockStateMessagePack>(eventMessage.Payload);
                latestStateData = stateData;
                var detail = stateData.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
                if (transitionStateData == null &&
                    detail.PreviousStateType == VanillaMachineBlockStateConst.IdleState &&
                    detail.CurrentStateType == VanillaMachineBlockStateConst.ProcessingState)
                {
                    transitionStateData = stateData;
                }
            }
            Assert.IsNotNull(transitionStateData, "idle→processingのChangeBlockStateイベントが取得できません。");
            Assert.AreEqual(0, transitionStateData.Position.X);
            Assert.AreEqual(0, transitionStateData.Position.Y);

            // 加工中の最新イベントでは供給率1の実効電力が反映されている
            // The latest processing event reflects the effective power at supply rate 1
            var latestDetail = latestStateData.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, latestDetail.CurrentStateType);
            Assert.AreEqual(1.0f, latestDetail.PowerRate);
        }
    }
}
