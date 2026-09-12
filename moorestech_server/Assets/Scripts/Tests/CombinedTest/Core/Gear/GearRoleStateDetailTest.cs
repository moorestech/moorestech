using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Gear
{
    /// <summary>
    /// 歯車の役割がサーバーのステート詳細に載り、IGearGenerator実装有無で決まることを固定する
    /// Pins that the gear role rides the server state detail and is settled by whether IGearGenerator is implemented
    /// </summary>
    public class GearRoleStateDetailTest
    {
        [Test]
        public void GeneratorPublishesGeneratorRoleAndMachinePublishesConsumerRole()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 発電機と歯車機械を直結して同じ網に入れる
            // Connect a generator and a gear machine directly so they share one network
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.GearMachine, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            generator.SetGenerateRpm(10f);
            generator.SetGenerateTorque(100f);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(GearRole.Generator, ReadGearStateDetail(generatorBlock).Role);
            Assert.AreEqual(GearRole.Consumer, ReadGearStateDetail(machineBlock).Role);
        }

        private static GearStateDetail ReadGearStateDetail(IBlock block)
        {
            var details = block.GetBlockState().CurrentStateDetails;
            return MessagePackSerializer.Deserialize<GearStateDetail>(details[GearStateDetail.BlockStateDetailKey]);
        }
    }
}
