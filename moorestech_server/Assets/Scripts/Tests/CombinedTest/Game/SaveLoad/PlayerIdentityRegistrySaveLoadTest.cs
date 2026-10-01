using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.PlayerIdentity;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.SaveLoad
{
    public class PlayerIdentityRegistrySaveLoadTest
    {
        [Test]
        public void 保存した身元と次のIDが別のコンテナに復元されるTest()
        {
            var saveProvider = SaveLoadPreparerTestFixture.CreateContainer();
            var registry = saveProvider.GetRequiredService<PlayerIdentityRegistry>();
            Assert.AreSame(registry, saveProvider.GetRequiredService<PlayerIdentityRegistry>());
            PlayerIdentityTestHelper.Register(registry, "steam:1");
            PlayerIdentityTestHelper.Register(registry, "steam:2");
            var json = saveProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            // 別コンテナで保存前の割当と採番の続きを確認する
            // A fresh container must recover both existing assignments and the next allocation
            var loadProvider = SaveLoadPreparerTestFixture.CreateContainer();
            ((WorldLoaderFromJson)loadProvider.GetRequiredService<IWorldSaveDataLoader>()).Load(json);
            var restored = loadProvider.GetRequiredService<PlayerIdentityRegistry>();

            Assert.AreEqual(2, PlayerIdentityTestHelper.Register(restored, "steam:2").PlayerId);
            Assert.AreEqual(1, PlayerIdentityTestHelper.Register(restored, "steam:1").PlayerId);
            Assert.AreEqual(3, PlayerIdentityTestHelper.Register(restored, "steam:3").PlayerId);
        }

        [Test]
        public void 持ち主未定と候補は保存復元後も最初の接続だけへ渡されるTest()
        {
            var provider = SaveLoadPreparerTestFixture.CreateContainer();
            provider.GetRequiredService<PlayerIdentityRegistry>().Load(new PlayersSaveJsonObject(3, 2,
                new List<PlayerIdentityEntryJsonObject> { new(1, null), new(2, null) }));
            var json = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            var restoredProvider = SaveLoadPreparerTestFixture.CreateContainer();
            ((WorldLoaderFromJson)restoredProvider.GetRequiredService<IWorldSaveDataLoader>()).Load(json);
            var restored = restoredProvider.GetRequiredService<PlayerIdentityRegistry>();

            Assert.AreEqual(2, PlayerIdentityTestHelper.Register(restored, "steam:9").PlayerId);
            Assert.AreEqual(3, PlayerIdentityTestHelper.Register(restored, "steam:10").PlayerId);
            var saved = restored.GetSaveJsonObject();
            Assert.IsNull(saved.ClaimCandidatePlayerId);
            Assert.AreEqual(1, saved.Entries[0].PlayerId);
            Assert.IsNull(saved.Entries[0].Identity);
        }

        [Test]
        public void 新規ワールドの初期化は身元対応表も初期化するTest()
        {
            var provider = SaveLoadPreparerTestFixture.CreateContainer();
            var registry = provider.GetRequiredService<PlayerIdentityRegistry>();
            PlayerIdentityTestHelper.Register(registry, "steam:1");
            PlayerIdentityTestHelper.Register(registry, "steam:2");

            ((WorldLoaderFromJson)provider.GetRequiredService<IWorldSaveDataLoader>()).WorldInitialize();

            Assert.IsEmpty(registry.GetSaveJsonObject().Entries);
            Assert.AreEqual(1, PlayerIdentityTestHelper.Register(registry, "steam:3").PlayerId);
        }

        [Test]
        public void 現在版でplayers節が欠けていたら理由付きで拒否するTest()
        {
            var provider = SaveLoadPreparerTestFixture.CreateContainer();
            var save = JObject.Parse(provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            save.Remove("players");
            LogAssert.Expect(LogType.Error, new Regex("^セーブに players がありません"));

            var loader = (WorldLoaderFromJson)provider.GetRequiredService<IWorldSaveDataLoader>();
            var exception = Assert.Throws<InvalidOperationException>(() => loader.Load(save));

            StringAssert.Contains("players", exception.Message);
        }


    }
}
