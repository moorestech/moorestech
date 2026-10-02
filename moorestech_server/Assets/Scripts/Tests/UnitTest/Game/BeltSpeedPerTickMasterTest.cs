using System.IO;
using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Core.Master.Validator;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     ベルトの1tick整数速度のロードと範囲検証を確認する
    ///     Checks loading and range validation of the belt's integer per-tick speed
    /// </summary>
    public class BeltSpeedPerTickMasterTest
    {
        [Test]
        public void テストマスタのベルト速度をMasterHolderから引ける()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 通常ベルト・歯車ベルト・分岐器の値をJSON通りに読む
            // Read the plain belt, gear belt and splitter values as written in the JSON
            var belt = (BeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam;
            var gearBelt = (GearBeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam;
            var splitter = (GearBeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyorSplitter).BlockParam;
            Assert.AreEqual(6, belt.BeltSpeedPerTick);
            Assert.AreEqual(32, gearBelt.BeltSpeedPerTick);
            Assert.AreEqual(128, splitter.BeltSpeedPerTick);
        }

        [TestCase("TestBeltConveyor", 0)]
        [TestCase("TestBeltConveyor", BeltConstants.MaxSpeed + 1)]
        [TestCase("GearBeltConveyor", 0)]
        [TestCase("GearBeltConveyor", BeltConstants.MaxSpeed + 1)]
        public void 範囲外のベルト速度は検証エラーになる(string blockName, int beltSpeedPerTick)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, blockName)["beltSpeedPerTick"] = beltSpeedPerTick;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"[BlockMaster] Name:{blockName} has beltSpeedPerTick:{beltSpeedPerTick} outside 1..{BeltConstants.MaxSpeed}", errorLogs);
        }

        [TestCase(1)]
        [TestCase(BeltConstants.MaxSpeed)]
        public void 境界値のベルト速度は検証を通る(int beltSpeedPerTick)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestBeltConveyor")["beltSpeedPerTick"] = beltSpeedPerTick;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static JToken PrepareBlocksJson()
        {
            // 依存マスタ初期化しJTokenのみ変更
            // Initialize dependency masters, then edit only the in-test JToken
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }

        private static JToken FindBlockParam(JToken blocksJToken, string blockName)
        {
            return blocksJToken["data"].Children<JObject>().Single(block => (string)block["name"] == blockName)["blockParam"];
        }
    }
}
