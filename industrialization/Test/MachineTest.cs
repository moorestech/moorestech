using System;
using System.Threading;
using System.Threading.Tasks;
using industrialization.Installation.BeltConveyor;
using industrialization.Installation.Machine;
using industrialization.Item;
using NUnit.Framework;

namespace industrialization.Test
{
    public class MachineTest
    {
        [SetUp]
        public void Setup()
        {
        }

        //[Test]
        public void ItemProcessingTest(int installationID,int[] InputItemsID,int[] InputItemsAmount,int[] OutputItemID,int[] OutputItemAmount)
        {
            var data = new MachineInstallation(0, Guid.Empty, new BeltConveyor(0,Guid.Empty));
            Thread.Sleep(10000);
            Assert.AreEqual(10, 10);
        }
    }
}