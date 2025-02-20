using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
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
        ///     与えた量の流体が搬入、搬出されるかのテスト
        /// </summary>
        [Test]
        public void FluidTransportTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, out var fluidPipeBlock1);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            // fluidPipeのflowCapacityは10だから3倍の量の量の液体
            var fluidStack = new FluidStack(Guid.NewGuid(), 30f, FluidContainer.Empty, fluidPipe0.FluidContainer);
            fluidPipe0.FluidContainer.AddToPendingList(fluidStack, FluidContainer.Empty, out FluidStack? remainFluidStack);
            
            // fluidPipeのcapacityは100だから溢れない
            if (remainFluidStack.HasValue) Assert.Fail();
            
            //TODO: どこかのタイミングで仮想化する必要がある。このままだと実際にかかる時間分テストでも時間がかかる
            var startTime = DateTime.Now;
            // 全て搬入するのにかかる時間
            const float fillTime = 3f;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > fillTime) break;
            }
            
            
            Assert.AreEqual(0f, fluidPipe0.FluidContainer.TotalAmount, 1f);
            Assert.AreEqual(30f, fluidPipe1.FluidContainer.TotalAmount, 1f);
        }
        
        /// <summary>
        ///     最大流量を超えない量の流体が正しく搬入、搬出されるかのテスト
        /// </summary>
        [Test]
        public void RealtimeFluidTransportTest()
        {
            const float amount = 30f; // 搬入する液体の量
            const float pipeFlowCapacity = 10f;
            var fillTime = amount / pipeFlowCapacity; // 全て搬入するのにかかる時間
            
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, out var fluidPipeBlock1);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            // fluidPipeのflowCapacityは10だから3倍の量の量の液体
            var fluidStack = new FluidStack(Guid.NewGuid(), amount, FluidContainer.Empty, fluidPipe0.FluidContainer);
            fluidPipe0.FluidContainer.AddToPendingList(fluidStack, FluidContainer.Empty, out FluidStack? remainFluidStack);
            
            // fluidPipeのcapacityは100だから溢れない
            if (remainFluidStack.HasValue) Assert.Fail();
            
            //TODO: どこかのタイミングで仮想化する必要がある。このままだと実際にかかる時間分テストでも時間がかかる
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                
                // 輸送済み液体量
                var currentTransportedAmount = (pipeFlowCapacity * elapsedTime).TotalSeconds;
                Assert.AreEqual(amount - currentTransportedAmount, fluidPipe0.FluidContainer.TotalAmount, 1f);
                Assert.AreEqual(currentTransportedAmount, fluidPipe1.FluidContainer.TotalAmount, 1f);
                
                if (elapsedTime.TotalSeconds > fillTime) break;
            }
            
            Assert.AreEqual(0f, fluidPipe0.FluidContainer.TotalAmount, 1f);
            Assert.AreEqual(30f, fluidPipe1.FluidContainer.TotalAmount, 1f);
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
            Assert.AreEqual(10, option0.FlowCapacity);
            
            var option1 = connect0.Value.SelfOption as FluidConnectOption;
            Assert.IsNotNull(option1);
            Assert.False(option1.IsInflowBlocked);
            Assert.False(option1.IsOutflowBlocked);
            Assert.AreEqual(10, option1.FlowCapacity);
        }
    }
}