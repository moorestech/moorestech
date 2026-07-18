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
    /// ブロック詳細の DTO ⇔ WireFixtures の一致を C# 側から強制する
    /// Enforce DTO ⇔ WireFixtures equality for block details from the C# side
    /// </summary>
    public class WireContractBlockDetailTest
    {
        // gear+gearNetwork 合成・progress あり。ギア機械の presence 代表ケース
        // Gear + gearNetwork composition with progress present; the gear-machine presence case
        [Test]
        public void GearMachineFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "GearMachine",
                Identifier = "(0, 0, 0)",
                BlockName = "ギア機械",
                ItemSlots = new List<BlockItemSlotDto>
                {
                    new() { ItemId = 3, Count = 2 },
                    new() { ItemId = 0, Count = 0 },
                },
                FluidSlots = new List<BlockFluidSlotDto>(),
                Progress = 0.1,
                Machine = new MachineDetailDto
                {
                    RecipeGuid = "00000000-0000-0000-0000-000000000000",
                    CurrentState = "idle",
                    CurrentPower = 0f,
                    RequestPower = 0f,
                    SlotLayout = new SlotLayoutDto { Input = 1, Output = 1, Module = 0 },
                },
                Gear = new GearDetailDto { IsClockwise = true, CurrentRpm = 12.5f, CurrentTorque = 3f, BaseRpm = 20f, BaseTorque = 5f },
                GearNetwork = new GearNetworkDto { TotalRequiredGearPower = 60f, TotalGenerateGearPower = 100f, StopReason = "none" },
            };
            AssertMatchesFixture(dto, "block_inventory_gear_machine.json");
        }

        // machine+electricNetwork+fluid+progress の電気機械 presence ケース
        // Electric machine with machine + electricNetwork + fluid + progress (presence case)
        [Test]
        public void ElectricMachineFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "ElectricMachine",
                Identifier = "(1, 0, 2)",
                BlockName = "電気機械",
                ItemSlots = new List<BlockItemSlotDto>
                {
                    new() { ItemId = 3, Count = 5 },
                    new() { ItemId = 0, Count = 0 },
                    new() { ItemId = 7, Count = 1 },
                    new() { ItemId = 0, Count = 0 },
                },
                FluidSlots = new List<BlockFluidSlotDto>
                {
                    new() { FluidId = 1, Amount = 25.5, Capacity = 100, Name = "水" },
                },
                Progress = 0.42,
                Machine = new MachineDetailDto
                {
                    RecipeGuid = "00000000-0000-0000-0000-000000000000",
                    CurrentState = "processing",
                    CurrentPower = 80f,
                    RequestPower = 100f,
                    SlotLayout = new SlotLayoutDto { Input = 2, Output = 1, Module = 1 },
                },
                ElectricNetwork = new ElectricNetworkDto { TotalGeneratePower = 500f, TotalRequiredPower = 300f, ConsumerCount = 4, PowerRate = 1f },
            };
            AssertMatchesFixture(dto, "block_inventory_machine.json");
        }

        // progress 省略ケース。generator+electricNetwork で progress を null にして omission 検証
        // Progress omission case; generator + electricNetwork with null progress to verify omission
        [Test]
        public void ElectricGeneratorFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "ElectricGenerator",
                Identifier = "(5, 0, 5)",
                BlockName = "発電機",
                ItemSlots = new List<BlockItemSlotDto> { new() { ItemId = 9, Count = 30 } },
                FluidSlots = new List<BlockFluidSlotDto>(),
                Progress = null,
                Generator = new GeneratorDetailDto { RemainingFuelTime = 12.5, CurrentFuelTime = 30, OperatingRate = 0.75f },
                ElectricNetwork = new ElectricNetworkDto { TotalGeneratePower = 200f, TotalRequiredPower = 150f, ConsumerCount = 2, PowerRate = 1f },
            };
            AssertMatchesFixture(dto, "block_inventory_generator.json");
        }

        // miner+miningItems+electricNetwork+progress の採掘機 presence ケース
        // Electric miner with miner + miningItems + electricNetwork + progress (presence case)
        [Test]
        public void ElectricMinerFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "ElectricMiner",
                Identifier = "(3, 0, 8)",
                BlockName = "電動採掘機",
                ItemSlots = new List<BlockItemSlotDto>
                {
                    new() { ItemId = 11, Count = 42 },
                    new() { ItemId = 0, Count = 0 },
                },
                FluidSlots = new List<BlockFluidSlotDto>(),
                Progress = 0.66,
                Miner = new MinerDetailDto
                {
                    CurrentPower = 50f,
                    RequestPower = 100f,
                    MiningItems = new List<MiningItemDto> { new() { ItemId = 11, ItemsPerMinute = 12f } },
                },
                ElectricNetwork = new ElectricNetworkDto { TotalGeneratePower = 100f, TotalRequiredPower = 100f, ConsumerCount = 1, PowerRate = 1f },
            };
            AssertMatchesFixture(dto, "block_inventory_miner.json");
        }

        // filterSplitter 単独。progress/詳細なしで方向配列を含む presence ケース
        // FilterSplitter only; a presence case with a direction array and no progress/other details
        [Test]
        public void FilterSplitterFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "FilterSplitter",
                Identifier = "(2, 0, 2)",
                BlockName = "フィルタ分岐器",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                FilterSplitter = new FilterSplitterDto
                {
                    DirectionCount = 3,
                    FilterSlotCountPerDirection = 2,
                    Directions = new List<FilterSplitterDirectionDto>
                    {
                        new() { Mode = "whitelist", FilterItemIds = new List<int> { 4, 0 } },
                        new() { Mode = "default", FilterItemIds = new List<int> { 0, 0 } },
                        new() { Mode = "blacklist", FilterItemIds = new List<int> { 7, 8 } },
                    },
                },
            };
            AssertMatchesFixture(dto, "block_inventory_filter_splitter.json");
        }

        // DTO を実運用シリアライザで直列化しフィクスチャと DeepEquals 照合する
        // Serialize the DTO with the production serializer and DeepEquals against the fixture
        private void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var expected = JToken.Parse(LoadFixture(fixtureName));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }

        private string LoadFixture(string fixtureName)
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            return File.ReadAllText(path);
        }
    }
}
