using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
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
    }
}
