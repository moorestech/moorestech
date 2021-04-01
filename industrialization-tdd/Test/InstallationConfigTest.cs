using industrialization.Installation;
using NUnit.Framework;

namespace industrialization.Test
{
    public class InstallationConfigTest
    {
        
        [SetUp]
        public void Setup()
        {
        }

        [TestCase(0,"TestMachine1")]
        [TestCase(1,"aaa")]
        [TestCase(2,"bb")]
        [TestCase(3,"ccccc")]
        public void ConfigNameTest(int id,string ans)
        {
            string name = InstallationConfig.GetInstllationConfig(id).name;
            Assert.AreEqual(ans,name);
        }

        
        [TestCase(0,2)]
        [TestCase(1,5)]
        [TestCase(2,22)]
        [TestCase(3,25)]
        public void inventorySolotsTest(int id, int ans)
        {
            
            int slots = InstallationConfig.GetInstllationConfig(id).inventorySlot;
            Assert.AreEqual(ans,slots);
        }
    }
}