using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    /// 駅名行は駅マスタのブロックにだけ載り未受信は空文字、移送行は移送の状態があるときだけ載る（判定は State の TrainPlatformDetailReader）
    /// Pins that the name row lands only on station blocks (empty before arrival) and the transfer row only with a transfer state (decided by State's TrainPlatformDetailReader)
    /// </summary>
    public class TrainPlatformDetailDtoBuilderTest
    {
        // 実マスタの駅ブロック。サーバー状態の駅名・モードとマスタのスロット数がそのまま載る
        // The real station block from master: the server-sent name and mode and the master slot count land as-is
        [Test]
        public void FillCarriesServerNameModeAndSlotCountForMasterStationBlock()
        {
            var block = MasterBlock(ForUnitTestModBlockId.TestTrainStation);
            var dto = new BlockInventoryDto();
            TrainPlatformDetailDtoBuilder.Fill(dto, block.BlockType, block.BlockParam, new TrainStationNameStateDetail("north"), new TrainPlatformTransferStateDetail(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform));

            Assert.AreEqual("north", dto.TrainStation.Name);
            Assert.AreEqual("unloadToPlatform", dto.TrainPlatform.Mode);
            Assert.AreEqual(((TrainStationBlockParam)block.BlockParam).ItemSlotCount, dto.TrainPlatform.ItemSlotCount);
        }

        // 駅名状態が未着でも行は出し、nullでなく空文字にする。移送の状態が無ければ移送行は出さない
        // The name row is still emitted before the name state arrives, as an empty string; without a transfer state the transfer row is omitted
        [Test]
        public void FillFallsBackToEmptyStringBeforeTheNameArrives()
        {
            var block = MasterBlock(ForUnitTestModBlockId.TestTrainStation);
            var dto = new BlockInventoryDto();
            TrainPlatformDetailDtoBuilder.Fill(dto, block.BlockType, block.BlockParam, null, null);

            Assert.AreEqual(string.Empty, dto.TrainStation.Name);
            Assert.IsNull(dto.TrainPlatform);
        }

        // 貨物プラットフォームは駅ではないので駅名行を持たない
        // An item platform is not a station, so it carries no name row
        [Test]
        public void FillOmitsTheNameRowForNonStationMaster()
        {
            var block = MasterBlock(ForUnitTestModBlockId.TestTrainItemPlatform);
            var dto = new BlockInventoryDto();
            TrainPlatformDetailDtoBuilder.Fill(dto, block.BlockType, block.BlockParam, new TrainStationNameStateDetail("north"), new TrainPlatformTransferStateDetail(TrainPlatformTransferComponent.TransferMode.LoadToTrain));

            Assert.IsNull(dto.TrainStation);
            Assert.AreEqual("loadToTrain", dto.TrainPlatform.Mode);
            Assert.AreEqual(((TrainItemPlatformBlockParam)block.BlockParam).ItemSlotCount, dto.TrainPlatform.ItemSlotCount);
        }

        private static BlockMasterElement MasterBlock(BlockId blockId)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return MasterHolder.BlockMaster.GetBlockMaster(blockId);
        }

        // 駅は転送行と駅名行を同時に持つ。wire形状ごと固定する
        // A station carries both the transfer row and the name row; this pins the whole wire shape
        [Test]
        public void TrainStationFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "TrainStation",
                Identifier = "(8, 0, 4)",
                BlockGuid = "77777777-7777-4777-8777-777777777777",
                ItemSlots = new List<BlockItemSlotDto> { new() { ItemId = 3, Count = 4 } },
                FluidSlots = new List<BlockFluidSlotDto>(),
                TrainPlatform = new TrainPlatformDetailDto { Mode = "unloadToPlatform", ItemSlotCount = 5 },
                TrainStation = new TrainStationDetailDto { Name = "north" },
            };

            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_train_station.json");
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch\nexpected: {expected}\nactual: {actual}");
        }
    }
}
