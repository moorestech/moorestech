using industrialization.Core.Config.Installation;
using NUnit.Framework;

namespace industrialization.Core.Test
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
            string name = InstallationConfig.GetInstallationsConfig(id).Name;
            Assert.AreEqual(ans,name);
        }

        
        [TestCase(0,2)]
        [TestCase(1,5)]
        [TestCase(2,22)]
        [TestCase(3,25)]
        public void InputSlotsTest(int id, int ans)
        {
            int slots = InstallationConfig.GetInstallationsConfig(id).InputSlot;
            Assert.AreEqual(ans,slots);
        }
        [TestCase(0,1)]
        [TestCase(1,3)]
        [TestCase(2,1)]
        [TestCase(3,5)]
        public void OutputSlotsTest(int id, int ans)
        {
            int slots = InstallationConfig.GetInstallationsConfig(id).OutputSlot;
            Assert.AreEqual(ans,slots);
        }
    }
}