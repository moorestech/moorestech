using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// v8 マスタで開ける全 blockType が専用本文か意図的な標準表示のどちらかに分類される（Web の fixture をそのまま読む）
    /// Every openable v8 blockType is classified as a dedicated body or an intentional standard view (reads the Web fixture as-is)
    /// </summary>
    public class BlockRegistryCoverageTest
    {
        private const string FixturePath = "moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json";
        private static readonly HashSet<string> IntentionalGeneric = new()
        {
            "Chest", "CleanRoomAirFilter", "CleanRoomMachine", "ElectricGenerator", "ElectricMachine", "ElectricMiner", "ElectricPump",
            "FluidPipe", "FuelGearGenerator", "GearMachine", "GearMiner", "GearPump", "GearToElectricGenerator",
        };

        /// <web-test file="e2e/tests/block/blockRegistryCoverage.spec.ts">v8マスタで専用UIを持つ全blockTypeがレジストリまたは意図的Genericに分類される</web-test>
        [Test]
        public void 開ける全blockTypeが登録か意図的な標準表示に分類される()
        {
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "..", "..", FixturePath));
            var openable = JArray.Parse(json).Where(e => (bool)e["openable"]).Select(e => (string)e["blockType"]).Distinct();
            var missing = openable.Where(t => !BlockComponentRegistry.Registered.ContainsKey(t) && !IntentionalGeneric.Contains(t)).ToArray();
            CollectionAssert.IsEmpty(missing);
            Assert.IsFalse(BlockComponentRegistry.Registered.ContainsKey("GearEnergyTransformer"));
        }
    }
}
