using System.IO;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UnityEngine;

namespace Tests.CombinedTest.Game.RocketLaunchPad.Regression
{
    public class RocketLaunchPadDuplicateOutputTest
    {
        [Test]
        public void 同一成果二行を一枠に合算して検証し格納する()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var itemsName = new JsonFileName("items");
            var items = JObject.Parse(configs[0].JsonContents[itemsName]);
            var outputs = (JArray)items["orbitalPayloads"][0]["arrivalOutputs"];
            outputs[0]["count"] = 40;
            outputs.Add(outputs[0].DeepClone());
            configs[0].JsonContents[itemsName] = items.ToString();
            var blocksName = new JsonFileName("blocks");
            var blocks = JObject.Parse(configs[0].JsonContents[blocksName]);
            blocks["data"].Single(block => (string)block["name"] == "TestRocketLaunchPad")["blockParam"]["outputSlotCount"] = 1;
            configs[0].JsonContents[blocksName] = blocks.ToString();

            // 実ローダーの容量検証と実発射を同じマスタ入力で通す
            // Exercise the real capacity validator and actual launch with the same master input
            Assert.DoesNotThrow(() => MasterHolder.Load(new MasterJsonFileContainer(configs)));
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(6, inventory.GetSlotSize());
            Assert.AreEqual(80, inventory.GetItem(5).Count);
        }
        [Test]
        public void 空枠より同種の既存出力を先に埋め別成果も格納する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var itemsName = new JsonFileName("items");
            var items = JObject.Parse(configs[0].JsonContents[itemsName]);
            var outputs = (JArray)items["orbitalPayloads"][0]["arrivalOutputs"];
            outputs[0]["count"] = 50;
            var second = (JObject)outputs[0].DeepClone();
            second["itemGuid"] = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.RocketFuelId).ItemGuid.ToString();
            outputs.Add(second);
            configs[0].JsonContents[itemsName] = items.ToString();
            MasterHolder.Load(new MasterJsonFileContainer(configs));

            // 出力[空,A50]へ成果[A50,B50]を実際の建造・発射で格納する
            // Store results [A50,B50] into [empty,A50] through the real build and launch
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(6, ForUnitTestItemId.ComputeDataId, 50);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));
            Assert.AreEqual(ForUnitTestItemId.RocketFuelId, inventory.GetItem(5).Id);
            Assert.AreEqual(50, inventory.GetItem(5).Count);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, inventory.GetItem(6).Id);
            Assert.AreEqual(100, inventory.GetItem(6).Count);
        }
    }
}
