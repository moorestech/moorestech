using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Fluid
{
    /// <summary>
    ///     ボイドパイプ（全量消滅の終端）のテスト。ADR0056
    ///     Tests for the void pipe, a terminal sink that destroys every fluid reaching its inflow face. ADR 0056
    /// </summary>
    public class VoidPipeTest
    {
        private static FluidId WaterId => MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000001"));
        private static FluidId SteamId => MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000002"));

        // テスト用ボイドの inflow (0,0,-1) を +X に向ける回転（Euler(0,270,0)）
        // Rotation that turns the test void's (0,0,-1) inflow toward +X (Euler(0,270,0))
        private const BlockDirection VoidFacingPositiveX = BlockDirection.West;

        // 全量受入・残量0・保持なし
        // Accepts the full amount regardless of fluid kind, returns zero remainder and keeps nothing
        [Test]
        public void AddLiquidReturnsZeroRemainderAndHoldsNothing()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var voidPipe = new VoidPipeComponent();

            var waterRemain = voidPipe.AddLiquid(new FluidStack(123.4, WaterId), default);
            var steamRemain = voidPipe.AddLiquid(new FluidStack(99999, SteamId), default);

            Assert.AreEqual(0, waterRemain.Amount);
            Assert.AreEqual(WaterId, waterRemain.FluidId);
            Assert.AreEqual(0, steamRemain.Amount);
            Assert.AreEqual(SteamId, steamRemain.FluidId);
            Assert.AreEqual(0, voidPipe.GetFluidInventory().Count);
        }

        // 本体+コネクタのみ組立、セーブ無し
        // The template assembles only the void component and the connector, with no save component
        [Test]
        public void TemplateBuildsVoidComponentAndConnectorWithoutSaveState()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.VoidPipe, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var voidBlock));

            Assert.IsTrue(voidBlock.ExistsComponent<VoidPipeComponent>());
            Assert.IsTrue(voidBlock.ExistsComponent<BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>>());
            Assert.IsFalse(voidBlock.ExistsComponent<IBlockSaveState>());
        }

        // ボイド終端で詰まらず0まで減衰(R4)
        // A void at the end of a pipe run drains upstream to zero without clogging (R4)
        [Test]
        public void PipeChainDrainsCompletelyIntoVoid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ボイドの受け入れ面は -Z 側
            // The void's inflow face is on -Z
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var farPipeBlock);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var nearPipeBlock);
            world.TryAddBlock(ForUnitTestModBlockId.VoidPipe, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var farPipe = farPipeBlock.GetComponent<FluidPipeComponent>();
            var nearPipe = nearPipeBlock.GetComponent<FluidPipeComponent>();
            farPipe.AddLiquid(new FluidStack(80, WaterId), default);
            nearPipe.AddLiquid(new FluidStack(80, WaterId), default);

            // 面上限は flowCapacity 10 × tick秒。160 が抜けるまで十分な tick を回す
            // Face cap is flowCapacity 10 × seconds per tick; run enough ticks for 160 to drain
            for (var i = 0; i < 2000; i++) GameUpdater.UpdateOneTick();

            Assert.AreEqual(0, farPipe.GetAmount(), 0.01);
            Assert.AreEqual(0, nearPipe.GetAmount(), 0.01);
        }

        // 受入面外は非接続・内容量維持(R6)
        // A pipe adjacent to any face other than the inflow face is not connected and keeps its amount (R6)
        [Test]
        public void PipeOnNonInflowFaceIsNotConnected()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.VoidPipe, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var sidePipeBlock);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var backPipeBlock);

            var sidePipe = sidePipeBlock.GetComponent<FluidPipeComponent>();
            var backPipe = backPipeBlock.GetComponent<FluidPipeComponent>();
            sidePipe.AddLiquid(new FluidStack(50, WaterId), default);
            backPipe.AddLiquid(new FluidStack(50, WaterId), default);

            for (var i = 0; i < 200; i++) GameUpdater.UpdateOneTick();

            var sideConnector = sidePipeBlock.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>>();
            var backConnector = backPipeBlock.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>>();
            Assert.AreEqual(0, sideConnector.ConnectedTargets.Count);
            Assert.AreEqual(0, backConnector.ConnectedTargets.Count);
            Assert.AreEqual(50, sidePipe.GetAmount(), 0.01);
            Assert.AreEqual(50, backPipe.GetAmount(), 0.01);
        }

        // 機械出力→ボイド直排出でタンク空に
        // A machine's output tank drains directly into an adjacent void
        [Test]
        public void MachineOutputDrainsDirectlyIntoVoid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 機械North出力は(-1,0,0)向き
            // The machine facing North outputs toward (-1,0,0)
            // ボイドは受け入れ面を+X（機械側）へ向けて設置
            // Place the void with its inflow face toward +X (the machine)
            world.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            world.TryAddBlock(ForUnitTestModBlockId.VoidPipe, new Vector3Int(-1, 0, 0), VoidFacingPositiveX, Array.Empty<BlockCreateParam>(), out _);

            var machineConnector = machineBlock.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>>();
            Assert.AreEqual(1, machineConnector.ConnectedTargets.Count);
            Assert.IsInstanceOf<VoidPipeComponent>(machineConnector.ConnectedTargets.Keys.First());

            var outputContainers = MachineFluidTestUtil.GetOutputFluidContainers(machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            outputContainers[0].AddLiquid(new FluidStack(40, WaterId));

            for (var i = 0; i < 200; i++) GameUpdater.UpdateOneTick();

            Assert.AreEqual(0, outputContainers[0].Amount, 0.01);
        }
    }
}
