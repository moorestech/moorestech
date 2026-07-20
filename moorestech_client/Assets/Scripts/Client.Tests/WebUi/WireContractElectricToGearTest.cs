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
    /// 回転生成機のwire契約を検証する
    /// Verifies the ElectricToGear capability DTO against the shared wire fixture
    /// </summary>
    public class WireContractElectricToGearTest
    {
        [Test]
        public void ElectricToGearFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "ElectricToGearGenerator",
                Identifier = "(4, 0, 2)",
                BlockName = "回転生成機",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                ElectricToGear = new ElectricToGearDetailDto
                {
                    SelectedIndex = 1,
                    FulfillmentRate = 0.75f,
                    ConsumedElectricPower = 10f,
                    OutputModes = new List<ElectricToGearOutputModeDto>
                    {
                        new() { Rpm = 10, Torque = 10, RequiredPower = 10 },
                        new() { Rpm = 20, Torque = 20, RequiredPower = 10 },
                    },
                },
            };

            // 共有fixtureと厳密照合する
            // Match the production serializer output exactly against the fixture shared with TypeScript
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(
                Application.dataPath,
                "Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_electric_to_gear.json");
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch\nexpected: {expected}\nactual: {actual}");
        }
    }
}
