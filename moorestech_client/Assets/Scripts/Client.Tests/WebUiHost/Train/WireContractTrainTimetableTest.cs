using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUiHost.Train
{
    // 時刻表取得状態のDTOとWireFixturesの一致を強制する
    // Enforce DTO ⇔ WireFixtures equality for the train inventory's timetable fetch state from the C# side
    public class WireContractTrainTimetableTest
    {
        [Test]
        public void LoadingTimetableFixtureMatchesDto()
        {
            var dto = CreateTrainInventory(TrainTimetableStateDto.Loading());
            dto.ItemSlots.Add(new BlockItemSlotDto { ItemId = 1, Count = 24 });
            dto.ItemSlots.Add(new BlockItemSlotDto { ItemId = 2, Count = 8 });
            AssertMatchesFixture(dto, "train_inventory.json");
        }

        // 取得済みのときだけ data が載る
        // data is carried only once the timetable is ready
        [Test]
        public void ReadyTimetableFixtureMatchesDto()
        {
            var position = new TrainStationPositionDto { X = 1, Y = 0, Z = 2 };
            var data = new TrainTimetableDto
            {
                TrainUnitId = "7",
                IsAutoRun = true,
                CurrentIndex = 0,
                Stops = new List<TrainTimetableStopDto> { new() { Position = position, Name = "North", Side = "front" } },
                Stations = new List<TrainTimetableStationDto> { new() { Position = position, Name = "North" } },
            };
            AssertMatchesFixture(CreateTrainInventory(TrainTimetableStateDto.Ready(data)), "train_inventory_timetable_ready.json");
        }

        [Test]
        public void UnavailableTimetableCarriesNoData()
        {
            var json = JObject.Parse(WebUiJson.Serialize(TrainTimetableStateDto.Unavailable()));
            Assert.That(json.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("{\"kind\":\"unavailable\"}"));
        }

        private static BlockInventoryDto CreateTrainInventory(TrainTimetableStateDto timetable)
        {
            return new BlockInventoryDto
            {
                Open = true,
                Source = "train",
                BlockType = "Train",
                Identifier = "101",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                Timetable = timetable,
            };
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}
