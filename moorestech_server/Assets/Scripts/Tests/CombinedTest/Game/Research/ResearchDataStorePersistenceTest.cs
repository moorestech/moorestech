using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Game.PlayerIdentity;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Game.Research
{
    public class ResearchDataStorePersistenceTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;

            // Research 1と2を完了させる
            CompleteResearchForTest(serviceProvider, Research1Guid, playerId);
            CompleteResearchForTest(serviceProvider, Research2Guid, playerId);
            
            // なにもクリアしていない状態でセーブ
            // Save without clearing anything
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var saveJson = assembleSaveJsonText.AssembleSaveJson();
            
            // ロード
            // load
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            
            var researchDataStore = loadServiceProvider.GetService<IResearchDataStore>();
            
            // Research 1, 2が完了していることを確認
            // Check that Research 1 and 2 are completed
            Assert.IsTrue(researchDataStore.IsResearchCompleted(Research1Guid));
            Assert.IsTrue(researchDataStore.IsResearchCompleted(Research2Guid));
        }


    }
}
