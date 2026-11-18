using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.Fluid
{
    /// <summary>
    ///     液体の色の形式の起動時検証を確かめる
    ///     Checks the startup validation of fluid color format
    /// </summary>
    public class FluidMasterColorValidationTest
    {
        [Test]
        public void 色として読めない文字列は検証で弾かれる()
        {
            var fluidsJToken = PrepareFluidsJson();
            FindFluid(fluidsJToken, "Water")["color"] = "not-a-color";

            AssertInvalidWith(fluidsJToken, "[FluidMaster] Name:Water has invalid Color:not-a-color (expected #RRGGBB)");
        }

        [Test]
        public void 桁の足りない色は検証で弾かれる()
        {
            var fluidsJToken = PrepareFluidsJson();
            FindFluid(fluidsJToken, "Water")["color"] = "#3399F";

            AssertInvalidWith(fluidsJToken, "[FluidMaster] Name:Water has invalid Color:#3399F (expected #RRGGBB)");
        }

        [Test]
        public void 末尾に改行の付いた色は検証で弾かれる()
        {
            // .NET の $ は末尾の改行の直前にも当たるので、\z で閉じていることを確かめる
            // .NET's $ also matches before a trailing newline, so check the pattern closes with \z
            var fluidsJToken = PrepareFluidsJson();
            FindFluid(fluidsJToken, "Water")["color"] = "#3399FF\n";

            AssertInvalidWith(fluidsJToken, "[FluidMaster] Name:Water has invalid Color:#3399FF");
        }

        [Test]
        public void 既存テストマスタの液体の色は検証を通る()
        {
            var isValid = FluidMasterUtil.Validate(new FluidMaster(PrepareFluidsJson()).Fluids, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static JToken PrepareFluidsJson()
        {
            // テスト用 mod の fluids.json を読み、JToken だけを書き換える
            // Read the test mod's fluids.json and edit only the in-test JToken
            var fluidsJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "fluids.json");
            return JToken.Parse(File.ReadAllText(fluidsJsonPath));
        }

        private static JObject FindFluid(JToken fluidsJToken, string fluidName)
        {
            return fluidsJToken["data"].Children<JObject>().First(fluid => (string)fluid["name"] == fluidName);
        }

        private static void AssertInvalidWith(JToken fluidsJToken, string expectedLog)
        {
            var isValid = FluidMasterUtil.Validate(new FluidMaster(fluidsJToken).Fluids, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains(expectedLog, errorLogs);
        }
    }
}
