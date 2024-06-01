using System.IO;
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

            var test1modId = "Test Author 1:testMod1";

            Assert.AreEqual("testItemJson1", loaded[test1modId].ItemConfigJson);
            Assert.AreEqual("testBlockJson1", loaded[test1modId].BlockConfigJson);
            Assert.AreEqual("", loaded[test1modId].MachineRecipeConfigJson);
            Assert.AreEqual("", loaded[test1modId].CraftRecipeConfigJson);

            var test2modId = "Test Author 2:testMod2";
            Assert.AreEqual("", loaded[test2modId].ItemConfigJson);
            Assert.AreEqual("", loaded[test2modId].BlockConfigJson);
            Assert.AreEqual("testMachineRecipeJson1", loaded[test2modId].MachineRecipeConfigJson);
            Assert.AreEqual("testCraftRecipeJson1", loaded[test2modId].CraftRecipeConfigJson);
        }
    }
}