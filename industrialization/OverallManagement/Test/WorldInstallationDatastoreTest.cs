using System;
using industrialization.Core;
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
            
            var intId = IntId.NewIntId();
            var i =  NormalMachineFactory.Create(1, intId, new NullIInstallationInventory());
            WorldInstallationDatastore.AddInstallation(i,1,1);
            var output = WorldInstallationDatastore.GetInstallation(intId);
            Assert.AreEqual(intId, output.IntId);

        }

        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            WorldInstallationDatastore.ClearData();
            
            var random = new Random(131513);
            for (int i = 0; i < 10; i++)
            {
                var intId = IntId.NewIntId();
                var ins =  NormalMachineFactory.Create(1, intId, new NullIInstallationInventory());

                int x = random.Next(-1000, 1000);
                int y = random.Next(-1000, 1000);
                
                WorldInstallationDatastore.AddInstallation(ins,x,y);
                var output = WorldInstallationDatastore.GetInstallation(x,y);
                Assert.AreEqual(intId, output.IntId);
            }
        }


        [Test]
        public void AlreadyRegisteredIntIDSecondTimeFailTest()
        {
            WorldInstallationDatastore.ClearData();
            
            var intId = IntId.NewIntId();
            var i =  NormalMachineFactory.Create(1, intId, new NullIInstallationInventory());
            WorldInstallationDatastore.AddInstallation(i,1,1);
            
            //座標だけ変えてintIDは同じ
            var i2 =  NormalMachineFactory.Create(1, intId, new NullIInstallationInventory());
            Assert.False(WorldInstallationDatastore.AddInstallation(i2,10,10));
        }

        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            WorldInstallationDatastore.ClearData();

            var i =  NormalMachineFactory.Create(1, IntId.NewIntId(), new NullIInstallationInventory());
            WorldInstallationDatastore.AddInstallation(i,1,1);
            
            //座標だけ変えてintIDは同じ
            var i2 =  NormalMachineFactory.Create(1, IntId.NewIntId(), new NullIInstallationInventory());
            Assert.False(WorldInstallationDatastore.AddInstallation(i2,1,1));
        }
    }
}