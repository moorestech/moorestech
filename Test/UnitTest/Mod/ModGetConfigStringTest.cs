#if NET6_0
using System;
using System.IO;
using Mod.Config;
using NUnit.Framework;

using Test.Module.TestMod;


namespace Test.UnitTest.Mod
{
    /// <summary>
    /// ConfigOnlyのtestConfigOnlyMod1と2をロードできるかテストするクラス
    /// zip、ディレクトリそれぞれロードできるかチェックする
    /// </summary>
    public class ModGetConfigStringTest
    {
        [Test]
        public void LoadConfigTest()
        {
            var (loaded,mod) = ModJsonStringLoader.GetConfigString(Path.Combine(TestModDirectory.ConfigOnlyDirectory, "mods"));
            
            Assert.AreEqual(loaded.Count, 2);

            var test1modId = "Test Author 1:testMod1"; 
            
            Assert.AreEqual("testItemJson1",loaded[test1modId].ItemConfigJson);
            Assert.AreEqual("testBlockJson1",loaded[test1modId].BlockConfigJson);
            Assert.AreEqual("",loaded[test1modId].MachineRecipeConfigJson);
            Assert.AreEqual("",loaded[test1modId].CraftRecipeConfigJson);
            
            var test2modId = "Test Author 2:testMod2"; 
            Assert.AreEqual("",loaded[test2modId].ItemConfigJson);
            Assert.AreEqual("",loaded[test2modId].BlockConfigJson);
            Assert.AreEqual("testMachineRecipeJson1",loaded[test2modId].MachineRecipeConfigJson);
            Assert.AreEqual("testCraftRecipeJson1",loaded[test2modId].CraftRecipeConfigJson);

        }
        
    }
}
#endif