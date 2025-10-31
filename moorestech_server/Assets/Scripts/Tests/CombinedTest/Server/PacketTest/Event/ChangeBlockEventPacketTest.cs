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
using UnityEngine;
using static Server.Protocol.PacketResponse.EventProtocol;
using System;

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
            //機械ブロックにアイテムを挿入するのでそのアイテムを挿入する
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var item1 = itemStackFactory.Create(new ItemId(1), 3);
            var item2 = itemStackFactory.Create(new ItemId(2), 1);
            
            var blockInventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            blockInventory.InsertItem(item1);
            blockInventory.InsertItem(item2);
            
            
            //稼働用の電気を供給する
            var electricMachineComponent = machine.GetComponent<VanillaElectricMachineComponent>();
            electricMachineComponent.SupplyEnergy(new ElectricPower(100));
            
            //最初にイベントをリクエストして、ブロードキャストを受け取れるようにする
            packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            
            //アップデートしてステートを更新する
            GameUpdater.UpdateWithWait();
            
            
            //ステートが実行中になっているかをチェック
            List<List<byte>> response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            var payLoad = eventMessagePack.Events[0].Payload;
            
            var changeStateData = MessagePackSerializer.Deserialize<BlockStateMessagePack>(payLoad);
            var stateDetail = changeStateData.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            
            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, stateDetail.PreviousStateType);
            Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, stateDetail.CurrentStateType);
            Assert.AreEqual(0, changeStateData.Position.X);
            Assert.AreEqual(0, changeStateData.Position.Y);
            
            var detailChangeData = changeStateData.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            Assert.AreEqual(1.0f, detailChangeData.PowerRate);
        }
    }
}
