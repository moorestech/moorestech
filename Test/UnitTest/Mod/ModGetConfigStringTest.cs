using System.IO;
using Mod.Config;
using NUnit.Framework;
using Test.Module.TestMod;

namespace Test.UnitTest.Mod
{
    public class ModGetConfigStringTest
    {
        public void LoadConfigTest()
        {
            var zipFileList = Directory.GetFiles(TestModDirectory.FolderDirectory, "*.zip");
            var loaded = ModJsonStringLoader.GetConfigString(zipFileList);
            
            Assert.AreEqual(loaded.Count, 2);
            Assert.AreEqual(loaded["testConfigOnlyMod1.zip"].ItemConfigJson, "testBlockJson1");
            Assert.AreEqual(loaded["testConfigOnlyMod1.zip"].BlockConfigJson, "testItemJson1");
            Assert.AreEqual(loaded["testConfigOnlyMod1.zip"].MachineRecipeConfigJson, "");
            Assert.AreEqual(loaded["testConfigOnlyMod1.zip"].CraftRecipeConfigJson, "");
            
            Assert.AreEqual(loaded["testConfigOnlyMod2.zip"].ItemConfigJson, "");
            Assert.AreEqual(loaded["testConfigOnlyMod2.zip"].BlockConfigJson, "");
            Assert.AreEqual(loaded["testConfigOnlyMod2.zip"].MachineRecipeConfigJson, "testCraftRecipeJson1");
            Assert.AreEqual(loaded["testConfigOnlyMod2.zip"].CraftRecipeConfigJson, "testMachineRecipeJson1");

        }
        
    }
}