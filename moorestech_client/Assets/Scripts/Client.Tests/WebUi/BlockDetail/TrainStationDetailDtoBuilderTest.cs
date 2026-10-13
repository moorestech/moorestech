using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    /// 駅名行は駅マスタのブロックにだけ載り、未受信は空文字になる
    /// Pins that the name row lands only on station-master blocks and degrades to an empty string, never null
    /// </summary>
    public class TrainStationDetailDtoBuilderTest
    {
        // 実マスタの駅ブロック。サーバー状態の駅名がそのまま載る
        // The real station block from master: the server-sent name lands as-is
        [Test]
        public void BuildStationDetailCarriesServerNameForMasterStationBlock()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockParam;
            var stationDto = TrainStationDetailDtoBuilder.BuildStationDetail(new TrainStationNameStateDetail("north"), param);

            Assert.AreEqual("north", stationDto.Name);
        }

        // 駅名状態が未着でも行は出し、nullでなく空文字にする
        // The row is still emitted before the name state arrives, as an empty string rather than null
        [Test]
        public void BuildStationDetailFallsBackToEmptyStringBeforeTheNameArrives()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockParam;
            var stationDto = TrainStationDetailDtoBuilder.BuildStationDetail(null, param);

            Assert.AreEqual(string.Empty, stationDto.Name);
        }

        // 貨物プラットフォームは駅ではないので駅名行を持たない
        // An item platform is not a station, so it carries no name row
        [Test]
        public void BuildStationDetailOmitsTheRowForNonStationMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainItemPlatform).BlockParam;

            Assert.IsNull(TrainStationDetailDtoBuilder.BuildStationDetail(new TrainStationNameStateDetail("north"), param));
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
