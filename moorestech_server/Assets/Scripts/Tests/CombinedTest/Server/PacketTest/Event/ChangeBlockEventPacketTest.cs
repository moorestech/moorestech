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

            //セグメント供給は機械Update後に走るため、遷移tickで満充電を観測できるよう電力をプリロードする
            //The segment supplies after the machine's update, so preload power to observe a full charge on the transition tick
            machine.GetComponent<VanillaElectricMachineComponent>().SupplyEnergy(new ElectricPower(100));

            //最初にイベントをリクエストして、ブロードキャストを受け取れるようにする
            packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0), new PacketResponseContext());

            //電力がセグメント経由で機械へ行き渡り稼働状態になるまで数tick進める
            //Advance several ticks until power propagates through the segment and the machine starts processing
            for (var i = 0; i < 3; i++) GameUpdater.UpdateOneTick();
            
            
            //ステートが実行中になっているかをチェック
            List<byte[]> response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0), new PacketResponseContext());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            // ブロックステート変更イベントを明示的に選択する
            // Select the change block state event explicitly
            var expectedTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(machine.BlockPositionInfo);
            var changeStateEvent = eventMessagePack.Events.Find(eventMessage => eventMessage.Tag == expectedTag);
            Assert.IsNotNull(changeStateEvent, "ChangeBlockStateイベントが取得できません。");
            var payLoad = changeStateEvent!.Payload;
            
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
