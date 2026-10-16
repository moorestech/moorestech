using System.IO;
using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Core.Master.Validator;
using Game.Block.Blocks.BeltConveyor.Topology;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     ベルトの秒速(1秒に進むマス数)のロード、1tick整数距離への換算、検証を確認する
    ///     Checks loading of the belt's cells-per-second, its conversion to a whole per-tick distance, and validation
    /// </summary>
    public class BeltSpeedPerSecondMasterTest
    {
        [Test]
        public void テストマスタのベルト秒速をMasterHolderから引き1tick距離へ換算できる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 通常ベルト・歯車ベルト・分岐器の値をJSON通りに読み、1tickの距離は6・36・180になる
            // Read the plain belt, gear belt and splitter values as written in the JSON; their per-tick distances are 6, 36 and 180
            var belt = (BeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam;
            var gearBelt = (GearBeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam;
            var splitter = (GearBeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyorSplitter).BlockParam;
            Assert.AreEqual(0.333333, belt.BeltSpeedPerSecond, 1e-6);
            Assert.AreEqual(2.0, gearBelt.BeltSpeedPerSecond, 1e-9);
            Assert.AreEqual(10, splitter.BeltSpeedPerSecond, 1e-9);
            Assert.AreEqual(6, BeltSpeedConversion.ToPerTick(belt.BeltSpeedPerSecond));
            Assert.AreEqual(36, BeltSpeedConversion.ToPerTick(gearBelt.BeltSpeedPerSecond));
            Assert.AreEqual(BeltConstants.MaxSpeed, BeltSpeedConversion.ToPerTick(splitter.BeltSpeedPerSecond));
        }

        // 検証をすり抜けた非整数の秒速は丸めずに換算で例外になる
        // A non-whole cells-per-second that slipped past validation throws instead of being rounded
        [TestCase(1.1)]
        [TestCase(3.3)]
        public void 整数にならない秒速は換算で例外になる(double cellsPerSecond)
        {
            Assert.Throws<System.InvalidOperationException>(() => BeltSpeedConversion.ToPerTick(cellsPerSecond));
        }

        [TestCase("TestBeltConveyor", 0.0)]
        [TestCase("TestBeltConveyor", 10.5)]
        [TestCase("GearBeltConveyor", 0.0)]
        [TestCase("GearBeltConveyor", 10.5)]
        public void 範囲外のベルト秒速は検証エラーになる(string blockName, double cellsPerSecond)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, blockName)["beltSpeedPerSecond"] = cellsPerSecond;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"[BlockMaster] Name:{blockName} has beltSpeedPerSecond:{cellsPerSecond} = {BeltSpeedConversion.ToPerTick(cellsPerSecond)} per tick, outside 1..{BeltConstants.MaxSpeed}", errorLogs);
        }

        // マスタはfloatで持つのでエラー文の値がずれないようfloatで正確な値を使う(×18で20.25と60.75)
        // The master stores floats, so use float-exact values to keep the logged value intact (x18 gives 20.25 and 60.75)
        [TestCase("TestBeltConveyor", 1.125)]
        [TestCase("GearBeltConveyor", 3.375)]
        public void 整数にならないベルト秒速は検証エラーになる(string blockName, double cellsPerSecond)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, blockName)["beltSpeedPerSecond"] = cellsPerSecond;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"[BlockMaster] Name:{blockName} has beltSpeedPerSecond:{cellsPerSecond} that is not a whole per-tick distance", errorLogs);
        }

        // 1tick距離1と上限180に当たる秒速、および小数6桁で書いた循環小数(40/tick)は通る
        // The cells-per-second for per-tick distances 1 and the cap 180, and a six-place repeating decimal (40 per tick), pass
        [TestCase(0.055556)]
        [TestCase(10.0)]
        [TestCase(2.222222)]
        public void 境界値のベルト秒速は検証を通る(double cellsPerSecond)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestBeltConveyor")["beltSpeedPerSecond"] = cellsPerSecond;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        // 小数6桁で書いた循環小数k/18は許容誤差内なので丸めた整数距離へ換算される
        // A repeating decimal k/18 written to six places is within tolerance and converts to the rounded whole distance
        [TestCase(0.055556, 1)]
        [TestCase(2.222222, 40)]
        [TestCase(6.666667, 120)]
        public void 小数6桁の循環小数の秒速は整数の1tick距離へ換算される(double cellsPerSecond, int expectedPerTick)
        {
            Assert.AreEqual(expectedPerTick, BeltSpeedConversion.ToPerTick(cellsPerSecond));
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
