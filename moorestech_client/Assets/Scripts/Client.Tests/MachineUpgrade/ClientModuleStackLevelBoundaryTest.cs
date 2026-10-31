using Client.Game.InGame.MachineUpgrade;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.MachineUpgrade
{
    public class ClientModuleStackLevelBoundaryTest
    {
        [TestCase(-1)]
        [TestCase(2)]
        public void InvalidSynchronizationPreservesCurrentLevelTest(int invalidLevel)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = new ClientModuleSlotStackLimitDatastore();
            IClientModuleSlotStackLimitMutation mutation = datastore;
            mutation.Apply(invalidLevel);
            Assert.AreEqual(0, datastore.CurrentLevel);
            Assert.AreEqual(3, datastore.CurrentStackLimit);
            mutation.Apply(1);
            mutation.Apply(invalidLevel);
            Assert.AreEqual(1, datastore.CurrentLevel);
            Assert.AreEqual(6, datastore.CurrentStackLimit);
        }
    }
}
