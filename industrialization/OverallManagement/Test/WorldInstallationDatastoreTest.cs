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
            WorldInstallationDatastore.ClearData();
            
            var guid = Guid.NewGuid();
            var i =  NormalMachineFactory.Create(1, guid, new NullIInstallationInventory());
            WorldInstallationDatastore.AddInstallation(i,1,1);
            var output = WorldInstallationDatastore.GetInstallation(guid);
            Assert.AreEqual(guid.ToString(), output.Guid.ToString());

        }

        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            WorldInstallationDatastore.ClearData();
            
            var random = new Random(131513);
            for (int i = 0; i < 10; i++)
            {
                
                var guid = Guid.NewGuid();
                var ins =  NormalMachineFactory.Create(1, guid, new NullIInstallationInventory());

                int x = random.Next(-1000, 1000);
                int y = random.Next(-1000, 1000);
                
                WorldInstallationDatastore.AddInstallation(ins,x,y);
                var output = WorldInstallationDatastore.GetInstallation(x,y);
                Assert.AreEqual(guid.ToString(), output.Guid.ToString());
            }
        }


        [Test]
        public void AlreadyRegisteredGUIDSecondTimeFailTest()
        {
            WorldInstallationDatastore.ClearData();
            
            var guid = Guid.NewGuid();
            var i =  NormalMachineFactory.Create(1, guid, new NullIInstallationInventory());
            WorldInstallationDatastore.AddInstallation(i,1,1);
            
            //座標だけ変えてguidは同じ
            var i2 =  NormalMachineFactory.Create(1, guid, new NullIInstallationInventory());
            Assert.False(WorldInstallationDatastore.AddInstallation(i2,10,10));
        }

        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            WorldInstallationDatastore.ClearData();

            var i =  NormalMachineFactory.Create(1, Guid.NewGuid(), new NullIInstallationInventory());
            WorldInstallationDatastore.AddInstallation(i,1,1);
            
            //座標だけ変えてguidは同じ
            var i2 =  NormalMachineFactory.Create(1, Guid.NewGuid(), new NullIInstallationInventory());
            Assert.False(WorldInstallationDatastore.AddInstallation(i2,1,1));
        }
    }
}