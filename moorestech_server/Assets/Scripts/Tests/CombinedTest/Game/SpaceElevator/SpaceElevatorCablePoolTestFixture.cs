using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    internal static class SpaceElevatorCablePoolTestFixture
    {
        internal static ServiceProvider CreateServer()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // Task 8の実装に依存せず、実ワールドの設置・撤去経路を通す
            // Exercise real world placement and removal independently of the Task 8 implementation
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase);
            ServerContext.BlockFactory.RegisterTemplateIBlock(master.BlockType, new TestBaseTemplate());
            return provider;
        }

        private sealed class TestBaseTemplate : IBlockTemplate
        {
            public IBlock New(BlockMasterElement master, BlockInstanceId id, BlockPositionInfo position, BlockCreateParam[] createParams)
            {
                return new BlockSystem(id, master.BlockGuid, new List<IBlockComponent> { new TestBase() }, position);
            }

            public IBlock Load(Dictionary<string, object> states, BlockMasterElement master, BlockInstanceId id, BlockPositionInfo position)
            {
                throw new NotSupportedException("この偽物は設置・撤去の検証専用です");
            }
        }

        private sealed class TestBase : ISpaceElevatorBase
        {
            public bool IsConnected { get; private set; }
            public bool IsDestroy { get; private set; }
            public void MarkConnected() { IsConnected = true; }
            public void Destroy() { IsDestroy = true; }
            // 接続可否は本番プールと接続サービスで検証し、偽物は受け取りだけ行う
            // The production pool and connection service decide availability; this fake only receives it
            public void SetCablePoolAvailability(int poolAmount, bool hasEnoughCable) { }
        }
    }
}
