using System;
using Core.Block;
using Core.Block.Machine.util;
using Core.Util;
using NUnit.Framework;
using World;

namespace Test.UnitTest.OverallManagement
{
    public class WorldBlockDatastoreTest
    {
        [Test]
        public void DataInsertAndOutputTest()
        {
            var worldData = new WorldBlockDatastore();
            
            var intId = IntId.NewIntId();
            var i =  NormalMachineFactory.Create(1, intId, new NullIBlockInventory());
            worldData.AddBlock(i,1,1,i);
            var output = worldData.GetBlock(intId);
            Assert.AreEqual(intId, output.GetIntId());

        }

        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            var worldData = new WorldBlockDatastore();
            
            var random = new Random(131513);
            for (int i = 0; i < 10; i++)
            {
                var intId = IntId.NewIntId();
                var ins =  NormalMachineFactory.Create(1, intId, new NullIBlockInventory());

                int x = random.Next(-1000, 1000);
                int y = random.Next(-1000, 1000);
                
                worldData.AddBlock(ins,x,y,ins);
                var output = worldData.GetBlock(x,y);
                Assert.AreEqual(intId, output.GetIntId());
            }
        }


        [Test]
        public void AlreadyRegisteredIntIdSecondTimeFailTest()
        {
            var worldData = new WorldBlockDatastore();
            
            var intId = IntId.NewIntId();
            var i =  NormalMachineFactory.Create(1, intId, new NullIBlockInventory());
            worldData.AddBlock(i,1,1,i);
            
            //座標だけ変えてintIDは同じ
            var i2 =  NormalMachineFactory.Create(1, intId, new NullIBlockInventory());
            Assert.False(worldData.AddBlock(i2,10,10,i2));
        }

        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var worldData = new WorldBlockDatastore();

            var i =  NormalMachineFactory.Create(1, IntId.NewIntId(), new NullIBlockInventory());
            worldData.AddBlock(i,1,1,i);
            
            //座標だけ変えてintIDは同じ
            var i2 =  NormalMachineFactory.Create(1, IntId.NewIntId(), new NullIBlockInventory());
            Assert.False(worldData.AddBlock(i2,1,1,i2));
        }
    }
}