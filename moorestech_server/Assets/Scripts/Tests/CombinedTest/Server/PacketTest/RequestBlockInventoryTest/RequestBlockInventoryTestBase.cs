using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public abstract class RequestBlockInventoryTestBase
    {
        protected const int InputSlotNum = 2;
        protected const int OutPutSlotNum = 3;
        // モジュールスロットは第3レンジとして統合スロット数に含まれる
        // Module slots are included in the unified slot count as the third range
        protected const int ModuleSlotNum = 4;

        protected byte[] RequestBlock(Vector3Int pos)
        {
            var identifier = InventoryIdentifierMessagePack.CreateBlockMessage(pos);
            return MessagePackSerializer.Serialize(new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier));
        }

        protected byte[] RequestTrain(TrainCarInstanceId trainCarInstanceId)
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarInstanceId.AsPrimitive());
            return MessagePackSerializer.Serialize(new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier));
        }
    }
}
