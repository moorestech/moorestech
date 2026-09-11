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

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     ボイドパイプ（受け入れ面に届いた流体を全量消滅させる終端）のテスト。ADR 0056
    ///     Tests for the void pipe, a terminal sink that destroys every fluid reaching its inflow face. ADR 0056
    /// </summary>
    public class VoidPipeTest
    {
        private static FluidId WaterId => MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000001"));
        private static FluidId SteamId => MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000002"));

        // テスト用ボイドの inflow (0,0,-1) を +X に向ける回転（Euler(0,270,0)）
        // Rotation that turns the test void's (0,0,-1) inflow toward +X (Euler(0,270,0))
        private const BlockDirection VoidFacingPositiveX = BlockDirection.West;

        // 流体種別を問わず全量を受け、残量0を返す。内容は何も保持しない
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

        // テンプレートはボイド本体とコネクタだけを組み立て、セーブコンポーネントを持たない
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

        // パイプ列の終端にボイドを置くと、上流の内容量が0まで減り続けて詰まらない（R4）
        // A void at the end of a pipe run keeps draining the upstream amount to zero without clogging (R4)
        [Test]
        public void PipeChainDrainsCompletelyIntoVoid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // (0,0,-2) → (0,0,-1) → ボイド(0,0,0)。ボイドの受け入れ面は -Z 側
            // (0,0,-2) → (0,0,-1) → void at (0,0,0); the void's inflow face is on -Z
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

        // 受け入れ面以外に隣接したパイプはボイドに接続されず、内容量も減らない（R6）
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

        // 機械の出力タンクからボイドへ直接排出され、タンクが空になる（R5）
        // A machine's output tank drains directly into an adjacent void and ends up empty (R5)
        [Test]
        public void MachineOutputDrainsDirectlyIntoVoid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 機械 (0,0,0)・North の outflow は (-1,0,0) 向き。ボイドは (-1,0,0) に、受け入れ面が +X（機械側）を向く回転で置く
            // The machine at (0,0,0) facing North outputs toward (-1,0,0); place the void there rotated so its inflow faces +X (the machine)
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
