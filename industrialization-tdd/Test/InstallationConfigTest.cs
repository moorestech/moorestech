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

        [Test]
        public void Test1()
        {
            string name = InstallationConfig.GetMachineData(0).name;
            Assert.AreEqual("TestMachine1","");
        }
    }
}