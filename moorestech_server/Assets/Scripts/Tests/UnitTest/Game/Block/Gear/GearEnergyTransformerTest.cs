using System.Collections.Generic;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using NUnit.Framework;
using Core.Update;
using UnityEngine;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Tests.UnitTest.Game.Block.Gear
{
    public class GearEnergyTransformerTest
    {
        [SetUp]
        public void Setup()
        {
            new GearNetworkDatastore(); 
        }

        [Test]
        public void Update_OverloadRpm_DestroysBlock()
        {
            var blockId = new BlockInstanceId(1);
            var mockConnector = new MockConnector();
            var mockRemover = new MockBlockRemover();
            
            var config = new GearOverloadConfig
            {
                MaxRpm = 100,
                MaxTorque = 100,
                CheckInterval = 0.0f, 
                BaseProb = 1.0f 
            };
            
            var testTransformer = new TestGearEnergyTransformer(
                new Torque(10), 
                config, 
                mockRemover, 
                blockId, 
                mockConnector
            );
            
            testTransformer.SetRpm(new RPM(200)); // 2x overload
            testTransformer.SetTorque(new Torque(10)); // Normal
            
            ((IUpdatableBlockComponent)testTransformer).Update();
            
            Assert.AreEqual(1, mockRemover.RemoveCalls.Count);
            Assert.AreEqual(blockId, mockRemover.RemoveCalls[0].id);
            Assert.AreEqual(BlockRemoveReason.Broken, mockRemover.RemoveCalls[0].reason);
        }
        
        public class MockBlockRemover : IBlockRemover
        {
            public List<(BlockInstanceId id, BlockRemoveReason reason)> RemoveCalls = new();
            public void Remove(BlockInstanceId blockInstanceId, BlockRemoveReason reason)
            {
                RemoveCalls.Add((blockInstanceId, reason));
            }
        }
        
        public class MockConnector : IBlockConnectorComponent<IGearEnergyTransformer>
        {
            public bool IsDestroy { get; }
            public void Destroy() { }
            public IReadOnlyDictionary<IGearEnergyTransformer, ConnectedInfo> ConnectedTargets => new Dictionary<IGearEnergyTransformer, ConnectedInfo>();
        }

        public class TestGearEnergyTransformer : GearEnergyTransformer
        {
            private RPM _testRpm;
            private Torque _testTorque;
            
            public void SetRpm(RPM rpm) => _testRpm = rpm;
            public void SetTorque(Torque torque) => _testTorque = torque;
            
            public override RPM CurrentRpm => _testRpm;
            public override Torque CurrentTorque => _testTorque;
            
            public TestGearEnergyTransformer(Torque requiredTorque, GearOverloadConfig config, IBlockRemover remover, BlockInstanceId id, IBlockConnectorComponent<IGearEnergyTransformer> connector)
                : base(requiredTorque, config, remover, id, connector) { }
        }
    }
}
