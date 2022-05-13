using System;
using System.IO;
using NUnit.Framework;
using Server.StartServerSystem;
using Test.Module.TestMod;

namespace Test.UnitTest.Mod
{
    public class ModGetConfigStringTest
    {
        [Test]
        public void LoadConfigTest()
        {
            var zipFileList = Directory.GetFiles(TestModDirectory.ConfigOnlyDirectory, "*.zip");
            Assert.AreEqual(zipFileList.Length, 2);
            var loaded = ModJsonStringLoader.GetConfigString(zipFileList);
            
            Assert.AreEqual(loaded.Count, 2);
            
            Assert.AreEqual("testItemJson1",loaded["testConfigOnlyMod1"].ItemConfigJson);
            Assert.AreEqual("testBlockJson1",loaded["testConfigOnlyMod1"].BlockConfigJson);
            Assert.AreEqual("",loaded["testConfigOnlyMod1"].MachineRecipeConfigJson);
            Assert.AreEqual("",loaded["testConfigOnlyMod1"].CraftRecipeConfigJson);
            
            Assert.AreEqual("",loaded["testConfigOnlyMod2"].ItemConfigJson);
            Assert.AreEqual("",loaded["testConfigOnlyMod2"].BlockConfigJson);
            Assert.AreEqual("testMachineRecipeJson1",loaded["testConfigOnlyMod2"].MachineRecipeConfigJson);
            Assert.AreEqual("testCraftRecipeJson1",loaded["testConfigOnlyMod2"].CraftRecipeConfigJson);

        }
        
    }
}