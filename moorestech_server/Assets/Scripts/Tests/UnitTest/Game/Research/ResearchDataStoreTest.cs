using System;
using Core.Master;
using Game.Research;
using Game.Research.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.Research
{
    public class ResearchDataStoreTest
    {
        [Test]
        public void SaveLoadKeepsCompletedSet()
        {
            var (_, sp) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var inv = sp.GetService<Game.PlayerInventory.Interface.IPlayerInventoryDataStore>();
            var exec = sp.GetService<Game.Challenge.IGameActionExecutor>();
            var ev = new ResearchEvent();

            var store = new ResearchDataStore(inv, exec, ev);

            // pick one research guid from master
            var guid = MasterHolder.ResearchMaster.GetAllNodes()[0].ResearchNodeGuid;

            // manually load as completed
            store.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = new() { guid.ToString() } });
            Assert.True(store.IsResearchCompleted(guid));

            var save = store.GetSaveJsonObject();
            var store2 = new ResearchDataStore(inv, exec, ev);
            store2.LoadResearchData(save);
            Assert.True(store2.IsResearchCompleted(guid));
        }
    }
}

