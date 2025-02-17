using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class FluidTest
    {
        /// <summary>
        ///     1単位の流体が正しく搬入、搬出されるかのテスト
        ///     Test to 1unit of fluid is carried in and out correctly.
        /// </summary>
        [Test]
        public void FluidTransportTest()
        {
        }
        
        /// <summary>
        ///     FluidPipeを設置できるかテスト
        ///     Test to set FluidPipe
        /// </summary>
        [Test]
        public void SetFluidPipeTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            
            var fluidPipeBlock = blockFactory.Create(
                ForUnitTestModBlockId.FluidPipe,
                new BlockInstanceId(int.MaxValue),
                new BlockPositionInfo(
                    Vector3Int.zero,
                    BlockDirection.North,
                    Vector3Int.one
                )
            );
            
            // FluidPipeComponentの存在を確認
            // Check the existence of FluidPipeComponent
            Assert.True(fluidPipeBlock.ExistsComponent<FluidPipeComponent>());
        }
        
        [Test]
        public void FluidPipeConnectTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, out var fluidPipeBlock1);
            
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector0 = fluidPipeBlock0.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector1 = fluidPipeBlock1.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            
            // パイプ同士が接続されているかのテスト
            // Test if the pipes are connected
            KeyValuePair<IFluidInventory, ConnectedInfo> connect0 = fluidPipeConnector0.ConnectedTargets.First();
            Assert.AreEqual(fluidPipeBlock1, connect0.Value.TargetBlock);
            
            KeyValuePair<IFluidInventory, ConnectedInfo> connect1 = fluidPipeConnector1.ConnectedTargets.First();
            Assert.AreEqual(fluidPipeBlock0, connect1.Value.TargetBlock);
            
            // 正しくオプションが読み込まれているかのテスト
            // Test if the options are read correctly
            var option0 = connect0.Value.SelfOption as FluidConnectOption;
            Assert.IsNotNull(option0);
            Assert.False(option0.IsInflowBlocked);
            Assert.False(option0.IsOutflowBlocked);
            Assert.AreEqual(100, option0.FlowCapacity);
            
            var option1 = connect0.Value.SelfOption as FluidConnectOption;
            Assert.IsNotNull(option1);
            Assert.False(option1.IsInflowBlocked);
            Assert.False(option1.IsOutflowBlocked);
            Assert.AreEqual(100, option1.FlowCapacity);
        }
    }
}