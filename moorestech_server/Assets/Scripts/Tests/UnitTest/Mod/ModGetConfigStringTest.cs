using System.IO;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Mod
{
    /// <summary>
    ///     ConfigOnlyのtestConfigOnlyMod1と2をロードできるかテストするクラス
    ///     zip、ディレクトリそれぞれロードできるかチェックする
    /// </summary>
    public class ModGetConfigStringTest
    {
        [Test]
        public void LoadConfigTest()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ConfigOnlyDirectory, "mods"));
            var loaded = ModJsonStringLoader.GetConfigString(modResource);
            
            Assert.AreEqual(loaded.Count, 2);
            
            var test1modId = new ModId("Test Author 1:testMod1");
            //var test1Config = loaded.Find(x => x.ModId == test1modId);
            var test1Config = loaded["Test Author 1:testMod1"];
            Assert.AreEqual("testItemJson1", test1Config.JsonContents[new JsonFileName("item")]);
            Assert.AreEqual("testBlockJson1", test1Config.JsonContents[new JsonFileName("block")]);
            
            var test2modId = new ModId("Test Author 2:testMod2");
            //var test2Config = loaded.Find(x => x.ModId == test2modId);
            var test2Config = loaded["Test Author 2:testMod2"];
            Assert.AreEqual("testMachineRecipeJson1", test2Config.JsonContents[new JsonFileName("machineRecipe")]);
            Assert.AreEqual("testCraftRecipeJson1", test2Config.JsonContents[new JsonFileName("craftRecipe")]);
        }
    }
}