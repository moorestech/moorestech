using System;
using industrialization.Core.Installation;
using industrialization.Core.Installation.Machine;
using industrialization.Core.Installation.Machine.util;
using industrialization.OverallManagement.DataStore;
using NUnit.Framework;

namespace industrialization.OverallManagement.Test
{
    public class WorldInstallationDatastoreTest
    {
        [Test]
        public void DataInsertAndOutputTest()
        {
            var guid = Guid.NewGuid();
            var i =  NormalMachineFactory.Create(1, guid, new NullIInstallationInventory());
            WorldInstallationDatastore.AddInstallation(i,1,1);
            var output = WorldInstallationDatastore.GetInstallation(guid);
            Assert.AreEqual(guid.ToString(), output.Guid.ToString());

        }
    }
}