using System;
using System.IO;
using Mod.Config;
using NUnit.Framework;

using Test.Module.TestMod;

namespace Test.UnitTest.Mod
{
    public class ModGetConfigStringTest
    {
        [Test]
        public void LoadConfigTest()
        {
            var loaded = ModJsonStringLoader.GetConfigString(TestModDirectory.ConfigOnlyDirectory);
            
            Assert.AreEqual(loaded.Count, 2);
            
            Assert.AreEqual("testItemJson1",loaded["testMod1"].ItemConfigJson);
            Assert.AreEqual("testBlockJson1",loaded["testMod1"].BlockConfigJson);
            Assert.AreEqual("",loaded["testMod1"].MachineRecipeConfigJson);
            Assert.AreEqual("",loaded["testMod1"].CraftRecipeConfigJson);
            
            Assert.AreEqual("",loaded["testMod2"].ItemConfigJson);
            Assert.AreEqual("",loaded["testMod2"].BlockConfigJson);
            Assert.AreEqual("testMachineRecipeJson1",loaded["testMod2"].MachineRecipeConfigJson);
            Assert.AreEqual("testCraftRecipeJson1",loaded["testMod2"].CraftRecipeConfigJson);

        }
        
    }
}