using System;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Master;
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
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class FluidTest
    {
        public static readonly Guid FluidGuid = new("00000000-0000-0000-1234-000000000001");
        public static FluidId FluidId => MasterHolder.FluidMaster.GetFluidId(FluidGuid);
        
        /// <summary>
        ///     与えた量の流体が搬入、搬出されるかのテスト
        /// </summary>
        [Test]
        public void FluidTransportTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            // fluidPipeのflowCapacityは10だから3倍の量の量の液体
            const double addingAmount = 30;
            var addingStack = new FluidStack(addingAmount, FluidId);
            var remainAmount = fluidPipe0.AddLiquid(addingStack, FluidContainer.Empty);
            
            // fluidPipeのcapacityは100だから溢れない
            if (remainAmount.Amount > 0) Assert.Fail();
            
            // tick数でループを制御（3秒 = 60 tick）
            // Loop controlled by tick count (3 seconds = 60 ticks)
            const int fillTicks = 60;
            for (var i = 0; i < fillTicks; i++)
            {
                GameUpdater.AdvanceTicks(1);
            }
            
            Assert.AreEqual(0f, fluidPipe0.GetAmount(), 1f);
            Assert.AreEqual(30f, fluidPipe1.GetAmount(), 1f);
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
            
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            // fluidPipeのflowCapacityは10だから3倍の量の量の液体
            var addingStack = new FluidStack(amount, FluidId);
            var remainAmount = fluidPipe0.AddLiquid(addingStack, FluidContainer.Empty);
            
            // fluidPipeのcapacityは100だから溢れない
            if (remainAmount.Amount > 0) Assert.Fail();
            
            // tick数でループを制御（3秒 = 60 tick）
            // Loop controlled by tick count (3 seconds = 60 ticks)
            const int fillTicks = 60;
            for (var tick = 0; tick < fillTicks; tick++)
            {
                GameUpdater.AdvanceTicks(1);

                // 経過秒数（tick × SecondsPerTick）
                // Elapsed seconds (tick × SecondsPerTick)
                var elapsedSeconds = (tick + 1) * GameUpdater.SecondsPerTick;

                // 輸送済み液体量
                // Transported fluid amount
                var currentTransportedAmount = pipeFlowCapacity * elapsedSeconds;
                Assert.AreEqual(amount - currentTransportedAmount, fluidPipe0.GetAmount(), 1f);
                Assert.AreEqual(currentTransportedAmount, fluidPipe1.GetAmount(), 1f);
            }
            
            Assert.AreEqual(0f, fluidPipe0.GetAmount(), 1f);
            Assert.AreEqual(30f, fluidPipe1.GetAmount(), 1f);
        }
        
        /// <summary>
        ///     FluidPipeを設置できるかテスト
        ///     Test to set FluidPipe
        /// </summary>
        [Test]
        public void SetFluidPipeTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
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
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            
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
            var option0 = connect0.Value.SelfConnector?.ConnectOption as FluidConnectOption;
            Assert.IsNotNull(option0);
            Assert.AreEqual(10, option0.FlowCapacity);

            var option1 = connect0.Value.SelfConnector?.ConnectOption as FluidConnectOption;
            Assert.IsNotNull(option1);
            Assert.AreEqual(10, option1.FlowCapacity);
        }
        
        /// <summary>
        ///     一方向のみに流れる設定が機能しているかテスト
        /// </summary>
        [Test]
        public void FlowBlockTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OneWayFluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oneWayFluidPipeBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            
            BlockConnectorComponent<IFluidInventory> oneWayFluidPipeConnector = oneWayFluidPipeBlock.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var oneWayFluidPipe = oneWayFluidPipeBlock.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            // oneWayFluidPipeの東（x+）方向にしか流れない設定になっていることを確認
            {
                // 出力
                var connect = oneWayFluidPipeConnector.ConnectedTargets[fluidPipe1];
                var selfOption = connect.SelfConnector?.ConnectOption as FluidConnectOption;
                Assert.NotNull(selfOption);
            }
            {
                // 入力
                Assert.False(oneWayFluidPipeConnector.ConnectedTargets.ContainsKey(fluidPipe0));
            }
            
            // 10fは1秒間に流れる流体の量
            const double addingAmount = 10d;
            var addingStack = new FluidStack(addingAmount, FluidId);
            var remainAmount = fluidPipe0.AddLiquid(addingStack, FluidContainer.Empty);
            
            Assert.AreEqual(0, remainAmount.Amount);
            
            // fluidPipe0からoneWayFluidPipe、fluidPipe1に流れる
            // oneWayFluidPipeはfluidPipe1方向にしか流れないからfluidPipe0には流れない
            // よって、ある程度時間が経つと全ての液体がfluidPipe1でとどまる
            // tick数でループを制御（5秒 = 100 tick）
            // Loop controlled by tick count (5 seconds = 100 ticks)
            const int ticks = 100;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.AdvanceTicks(1);
            }
            
            Assert.AreEqual(0, fluidPipe0.GetAmount(), 0.01d);
            Assert.AreEqual(0, oneWayFluidPipe.GetAmount(), 0.01d);
            Assert.AreEqual(10, fluidPipe1.GetAmount(), 0.01d);
            Assert.AreEqual(FluidMaster.EmptyFluidId, fluidPipe0.GetFluidId());
            Assert.AreEqual(FluidMaster.EmptyFluidId, oneWayFluidPipe.GetFluidId());
        }
        
        /// <summary>
        ///     液体の総量が一定であることのテスト
        /// </summary>
        [Test]
        public void FluidTotalAmountTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OneWayFluidPipe, Vector3Int.right * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock2);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            
            // 10fは1秒間に流れる流体の量
            const double addingAmount = 10d;
            var addingStack = new FluidStack(addingAmount, FluidId);
            var remainAmount = fluidPipe0.AddLiquid(addingStack, FluidContainer.Empty);
            
            Assert.AreEqual(0, remainAmount.Amount);
            
            var totalAmount = fluidPipe0.GetAmount() + fluidPipe1.GetAmount() + fluidPipe2.GetAmount();
            
            // fluidPipe0からfluidPipe1に流れる
            // tick数でループを制御（10秒 = 200 tick）
            // Loop controlled by tick count (10 seconds = 200 ticks)
            const int ticks = 200;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.AdvanceTicks(1);
            }
            
            var lastTotalAmount = fluidPipe0.GetAmount() + fluidPipe1.GetAmount() + fluidPipe2.GetAmount();
            Assert.AreEqual(totalAmount, lastTotalAmount, 0.01d);
        }
        
        // 液体が複数のパイプへ正しく分割されるかのテスト
        [Test]
        public void FluidSplitTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 012 という並び
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock2);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            
            // 20fは1秒間に二つの方向へ流れる流体の量
            const double addingAmount = 20d;
            var addingStack = new FluidStack(addingAmount, FluidId);
            fluidPipe1.AddLiquid(addingStack, FluidContainer.Empty);

            // tick数でループを制御（1秒 = 20 tick）
            // Loop controlled by tick count (1 second = 20 ticks)
            const int ticks = 20;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.AdvanceTicks(1);
            }
            
            // 0と2に流れる
            Assert.AreEqual(10f, fluidPipe0.GetAmount(), 1d);
            Assert.AreEqual(10f, fluidPipe2.GetAmount(), 1d);
            
            // 総量をテスト
            Assert.AreEqual(20d, fluidPipe0.GetAmount() + fluidPipe1.GetAmount() + fluidPipe2.GetAmount(), 0.01d);
        }
        
        // 水が跳ね返ることの確認
        [Test]
        public void FluidBounceTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            // 10dは1秒間に流れる量
            const double amount = 10d;
            var addingStack = new FluidStack(amount, FluidId);
            fluidPipe0.AddLiquid(addingStack, FluidContainer.Empty);

            // tick数でループを制御（1秒 = 20 tick）
            // Loop controlled by tick count (1 second = 20 ticks)
            const int ticks = 20;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.AdvanceTicks(1);
            }

            // fluidPipe1に全て流れたことを確認
            Assert.AreEqual(0d, fluidPipe0.GetAmount(), 1d);
            Assert.AreEqual(10d, fluidPipe1.GetAmount(), 1d);

            // もう1秒待つ
            // Wait another second
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.AdvanceTicks(1);
            }
            
            // fluidPipe0に全て流れたことを確認
            Assert.AreEqual(10d, fluidPipe0.GetAmount(), 1d);
            Assert.AreEqual(0d, fluidPipe1.GetAmount(), 1d);
        }
        
        /// <summary>
        ///     異なる液体が混ざらないことのテスト
        /// </summary>
        [Test]
        public void FluidMixTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            var fluid0Guid = Guid.Parse("00000000-0000-0000-1234-000000000001");
            var fluid0 = MasterHolder.FluidMaster.GetFluidId(fluid0Guid);
            var fluid1Guid = Guid.Parse("00000000-0000-0000-1234-000000000002");
            var fluid1 = MasterHolder.FluidMaster.GetFluidId(fluid1Guid);
            
            const double fluid0Amount = 10d;
            const double fluid1Amount = 20d;
            
            fluidPipe0.AddLiquid(new FluidStack(fluid0Amount, fluid0), FluidContainer.Empty);
            fluidPipe1.AddLiquid(new FluidStack(fluid1Amount, fluid1), FluidContainer.Empty);
            
            for (var i = 0; i < 10; i++)
            {
                GameUpdater.SpecifiedDeltaTimeUpdate(0.1);
                
                Assert.AreEqual(fluid0Amount, fluidPipe0.GetAmount());
                Assert.AreEqual(fluid1Amount, fluidPipe1.GetAmount());
            }
        }
        
        /// <summary>
        ///     液体の定義に水と蒸気があることのテスト
        /// </summary>
        [Test]
        public void FluidMasterTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var fluidMaster = MasterHolder.FluidMaster;
            
            var names = fluidMaster.GetAllFluidIds().Select(id => fluidMaster.GetFluidMaster(id)).ToDictionary(f => f.Name);
            
            Assert.Contains("Water", names.Keys);
            Assert.Contains("Steam", names.Keys);
        }
        
        /// <summary>
        ///     FluidPipeのIBlockStateObservableの実装のテスト
        /// </summary>
        [Test]
        public void FluidPipeNetworkTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            
            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            
            var fluidId = Guid.Parse("00000000-0000-0000-1234-000000000001");
            var fluid = MasterHolder.FluidMaster.GetFluidId(fluidId);
            
            const double fluid0Amount = 10d;
            const double fluid1Amount = 20d;
            
            fluidPipe0.AddLiquid(new FluidStack(fluid0Amount, fluid), FluidContainer.Empty);
            fluidPipe1.AddLiquid(new FluidStack(fluid1Amount, fluid), FluidContainer.Empty);
            
            var callCount = 0;
            
            fluidPipe0.OnChangeBlockState.Subscribe(_ =>
            {
                callCount++;
                Debug.Log("callCount " + callCount);
            });
            fluidPipe1.OnChangeBlockState.Subscribe(_ => { callCount++; });
            
            const int steps = 10;
            
            for (var i = 0; i < steps; i++) GameUpdater.SpecifiedDeltaTimeUpdate(0.1);
            
            // First update: no fluid movement (both pipes have receive flag from initial AddLiquid)
            // Updates 2-10: both pipes change state (sender and receiver)
            // Total: 0 + 9*2 = 18 state changes
            Assert.AreEqual(18, callCount);
        }
    }
    
    public static class FluidPipeExtension
    {
        public static FluidContainer GetFluidContainer(this FluidPipeComponent fluidPipe){
            // リフレクションでFluidContainerを取得する
            var field = typeof(FluidPipeComponent).GetField("_fluidContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            return (FluidContainer)field.GetValue(fluidPipe);
        }

        
        public static double GetAmount(this FluidPipeComponent fluidPipe){
            return fluidPipe.GetFluidContainer().Amount;
        }
        
        public static FluidId GetFluidId(this FluidPipeComponent fluidPipe)
        {
            return fluidPipe.GetFluidContainer().FluidId;
        }
        
    }
}
