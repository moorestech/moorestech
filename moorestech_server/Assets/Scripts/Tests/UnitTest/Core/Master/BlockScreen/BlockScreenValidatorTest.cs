using System;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.BlockScreen
{
    public class BlockScreenValidatorTest
    {
        [TestCase("chest")]
        [TestCase("true")]
        public void スキーマに無いブロック画面IDを止める(string unknownScreen)
        {
            var resource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            var configs = ModJsonStringLoader.GetMasterString(resource);
            var fileName = new JsonFileName("blocks");
            var blocks = JObject.Parse(configs[0].JsonContents[fileName]);

            // メモリ上の先頭画面IDだけ未知値に
            // Set only the first screen ID to an unknown value in memory.
            var firstBlock = blocks["data"].Children<JObject>().First();
            firstBlock["blockScreen"] = unknownScreen;
            configs[0].JsonContents[fileName] = blocks.ToString();

            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(new MasterJsonFileContainer(configs)));
            StringAssert.Contains($"Block:{(string)firstBlock["name"]} has unknown blockScreen:{unknownScreen}", error.Message);
        }

        [Test]
        public void テストマスタのブロック画面IDは検証を通る()
        {
            var resource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            var container = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(resource));

            Assert.DoesNotThrow(() => MasterHolder.Load(container));
        }
    }
}
