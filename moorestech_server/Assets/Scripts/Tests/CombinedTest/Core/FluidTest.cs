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
        public static readonly Guid FluidId = new("00000000-0000-0000-0000-000000001234");
        
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
            var fluidStack = new FluidStack(FluidId, 30f, FluidContainer.Empty, fluidPipe0.FluidContainer);
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
            var fluidStack = new FluidStack(FluidId, amount, FluidContainer.Empty, fluidPipe0.FluidContainer);
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
            Assert.AreEqual(10, option0.FlowCapacity);
            
            var option1 = connect0.Value.SelfOption as FluidConnectOption;
            Assert.IsNotNull(option1);
            Assert.AreEqual(10, option1.FlowCapacity);
        }
        
        /// <summary>
        ///     一方向のみに流れる設定が機能しているかテスト
        /// </summary>
        [Test]
        public void FlowBlockTest()
        {
            new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OneWayFluidPipe, Vector3Int.right * 1, BlockDirection.North, out var oneWayFluidPipeBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 2, BlockDirection.North, out var fluidPipeBlock1);
            
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector0 = fluidPipeBlock0.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> oneWayFluidPipeConnector = oneWayFluidPipeBlock.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector1 = fluidPipeBlock1.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var oneWayFluidPipe = oneWayFluidPipeBlock.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            // oneWayFluidPipeの東（x+）方向にしか流れない設定になっていることを確認
            {
                // 出力
                var connect = oneWayFluidPipeConnector.ConnectedTargets[fluidPipe1];
                var selfOption = connect.SelfOption as FluidConnectOption;
                Assert.NotNull(selfOption);
            }
            {
                // 入力
                Assert.False(oneWayFluidPipeConnector.ConnectedTargets.ContainsKey(fluidPipe0));
            }
            
            // 10fは1秒間に流れる流体の量
            var addingStack = new FluidStack(FluidId, 10f, FluidContainer.Empty, oneWayFluidPipe.FluidContainer);
            fluidPipe0.FluidContainer.AddToPendingList(addingStack, FluidContainer.Empty, out _);
            
            // fluidPipe0からoneWayFluidPipe、fluidPipe1に流れる
            // oneWayFluidPipeはfluidPipe1方向にしか流れないからfluidPipe0には流れない
            // よって、ある程度時間が経つと全ての液体がfluidPipe1でとどまる
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 5) break;
            }
            
            Assert.AreEqual(0, fluidPipe0.FluidContainer.TotalAmount);
            Assert.AreEqual(0, oneWayFluidPipe.FluidContainer.TotalAmount);
            Assert.AreEqual(10, fluidPipe1.FluidContainer.TotalAmount);
        }
        
        /// <summary>
        ///     液体の総量が一定であることのテスト
        /// </summary>
        [Test]
        public void FluidTotalAmountTest()
        {
            new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OneWayFluidPipe, Vector3Int.right * 2, BlockDirection.North, out var oneWayFluidPipeBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 3, BlockDirection.North, out var fluidPipeBlock2);
            
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector0 = fluidPipeBlock0.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector1 = fluidPipeBlock1.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> oneWayFluidPipeConnector = oneWayFluidPipeBlock.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector2 = fluidPipeBlock2.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var oneWayFluidPipe = oneWayFluidPipeBlock.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            
            // 10fは1秒間に流れる流体の量
            var addingStack = new FluidStack(FluidId, 10f, FluidContainer.Empty, fluidPipe0.FluidContainer);
            fluidPipe0.FluidContainer.AddToPendingList(addingStack, FluidContainer.Empty, out _);
            
            var totalAmount = fluidPipe0.FluidContainer.TotalAmount + fluidPipe1.FluidContainer.TotalAmount + fluidPipe2.FluidContainer.TotalAmount;
            
            // fluidPipe0からfluidPipe1に流れる
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 10) break;
            }
            
            var lastTotalAmount = fluidPipe0.FluidContainer.TotalAmount + fluidPipe1.FluidContainer.TotalAmount + fluidPipe2.FluidContainer.TotalAmount;
            Assert.AreEqual(totalAmount, lastTotalAmount);
        }
        
        // 液体が複数のパイプへ正しく分割されるかのテスト
        [Test]
        public void FluidSplitTest()
        {
            new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 012 という並び
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 2, BlockDirection.North, out var fluidPipeBlock2);
            
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector0 = fluidPipeBlock0.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector1 = fluidPipeBlock1.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            BlockConnectorComponent<IFluidInventory> fluidPipeConnector2 = fluidPipeBlock2.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            
            // 20fは1秒間に二つの方向へ流れる流体の量
            var addingStack = new FluidStack(FluidId, 20f, FluidContainer.Empty, FluidContainer.Empty);
            fluidPipe1.FluidContainer.AddToPendingList(addingStack, FluidContainer.Empty, out _);
            
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 1) break;
            }
            
            // 0と2に流れる
            Assert.AreEqual(10f, fluidPipe0.FluidContainer.TotalAmount);
            Assert.AreEqual(10f, fluidPipe2.FluidContainer.TotalAmount);
        }
    }
}