using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// Phase B2 のPFと電柱wire契約を検証する
    /// Verifies the Phase B2 train-platform and electric-pole wire contracts
    /// </summary>
    public class WireContractPhaseB2Test
    {
        [Test]
        public void TrainPlatformFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "TrainItemPlatform",
                Identifier = "(6, 0, 4)",
                BlockName = "貨物プラットフォーム",
                ItemSlots = new List<BlockItemSlotDto>
                {
                    new() { ItemId = 3, Count = 12 },
                    new() { ItemId = 0, Count = 0 },
                },
                FluidSlots = new List<BlockFluidSlotDto>(),
                TrainPlatform = new TrainPlatformDetailDto
                {
                    Mode = "loadToTrain",
                    ItemSlotCount = 2,
                },
            };

            AssertMatchesFixture(dto, "block_inventory_train_platform.json");
        }

        [Test]
        public void ElectricPoleFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "ElectricPole",
                Identifier = "(7, 0, 4)",
                BlockName = "電柱",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                ElectricNetwork = new ElectricNetworkDto
                {
                    TotalGeneratePower = 240f,
                    TotalRequiredPower = 180f,
                    ConsumerCount = 3,
                    PowerRate = 1f,
                },
            };

            AssertMatchesFixture(dto, "block_inventory_electric_pole.json");
        }

        [Test]
        public void TrainFluidPlatformFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "TrainFluidPlatform",
                Identifier = "(8, 0, 4)",
                BlockName = "液体プラットフォーム",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                TrainPlatform = new TrainPlatformDetailDto
                {
                    Mode = "unloadToPlatform",
                    FluidCapacity = 1000,
                },
            };

            AssertMatchesFixture(dto, "block_inventory_train_fluid_platform.json");
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            // 共有fixtureへ厳密照合する
            // Match the production serializer output exactly against the fixture shared with TypeScript
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(
                Application.dataPath,
                "Scripts/Client.Tests/WebUi/WireFixtures",
                fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(
                JToken.DeepEquals(expected, actual),
                $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}
