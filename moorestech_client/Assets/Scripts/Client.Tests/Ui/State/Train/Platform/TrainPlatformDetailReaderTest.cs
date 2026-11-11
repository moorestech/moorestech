using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Tests.Ui.State.Train.Platform
{
    public class TrainPlatformDetailReaderTest
    {
        private static readonly IReadOnlyList<SlotItem> Slots = new[] { new SlotItem(new ItemId(3), 12) };
        private static readonly IReadOnlyList<BlockFluidSlotState> NoFluids = new List<BlockFluidSlotState>();
        private static readonly TrainPlatformTransferStateDetail Unload = new(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

        [SetUp]
        public void SetUp()
        {
            // TestBlockParams がマスタのブロックを引くので、先にマスタを読む
            // TestBlockParams looks up master blocks, so load the master first
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 駅は名前と枠とモードを持つ()
        {
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainStation, TestBlockParams.Station(), new TrainStationNameStateDetail("北駅"), Unload, Slots, NoFluids);
            Assert.AreEqual("北駅", body.StationName.Value);
            Assert.AreSame(Slots, body.ItemSlots.Value);
            Assert.AreEqual(TrainPlatformTransferMode.UnloadToPlatform, body.Mode.Value);
        }

        [Test]
        public void 品物PFは名前を持たず枠とモードを持つ()
        {
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainItemPlatform, TestBlockParams.ItemPlatform(), null, Unload, Slots, NoFluids);
            Assert.IsFalse(body.StationName.IsPresent);
            Assert.AreSame(Slots, body.ItemSlots.Value);
        }

        [Test]
        public void 液体PFは枠を持たず液体とモードを持つ()
        {
            var fluids = new[] { BlockFluidSlotState.Empty(1000) };
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainFluidPlatform, TestBlockParams.FluidPlatform(), null, Unload, Slots, fluids);
            Assert.IsFalse(body.ItemSlots.IsPresent);
            Assert.AreSame(fluids, body.Fluids);
            Assert.AreEqual(TrainPlatformTransferMode.UnloadToPlatform, body.Mode.Value);
        }

        [Test]
        public void 移送の状態が届く前は枠もモードも出さず駅名は空()
        {
            // Web ホストは状態が無ければ詳細ごと出さない。駅名の StateDetail が無ければ空名
            // The Web host omits the detail without a state; a missing name StateDetail gives an empty name
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainStation, TestBlockParams.Station(), null, null, Slots, NoFluids);
            Assert.AreEqual(string.Empty, body.StationName.Value);
            Assert.IsFalse(body.ItemSlots.IsPresent);
            Assert.IsFalse(body.Mode.IsPresent);
        }
    }

    /// <summary>
    /// テスト用 mod のブロックマスタから種類ごとの最初の BlockParam を引く
    /// Looks up the first BlockParam of each type from the test mod's block master
    /// </summary>
    internal static class TestBlockParams
    {
        public static IBlockParam Station() => Find(BlockTypeConst.TrainStation);
        public static IBlockParam ItemPlatform() => Find(BlockTypeConst.TrainItemPlatform);
        public static IBlockParam FluidPlatform() => Find(BlockTypeConst.TrainFluidPlatform);

        private static IBlockParam Find(string blockType)
        {
            return Array.Find(MasterHolder.BlockMaster.Blocks.Data, b => b.BlockType == blockType).BlockParam;
        }
    }
}
