using System;
using System.Linq;
using Core.Master;
using Game.PlayerIdentity;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Game.Research
{
    // コスト0研究のリセット部品が頼る「解放記録が無ければマスタ初期値、完了済み研究の再実行で解放し直す」を実ロードで確かめる
    // Verify by a real load what the zero-cost research reset relies on: no record means the master default, and completed research re-grants on replay
    public class ResearchUnlockRecordFallbackLoadTest
    {
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void 解放記録を消したアイテムは完了済み研究の再実行でだけ解放されるTest(bool keepResearchCompleted, bool expectedUnlocked)
        {
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // ロード時の検査が身元対応表に無いプレイヤーを拒否するので、研究するプレイヤーを登録する
            // The load-time check rejects players absent from the identity registry, so register the researching player
            var playerId = PlayerIdentityTestHelper.Register(saveProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            CompleteResearchForTest(saveProvider, Research4Guid, playerId);
            var save = JObject.Parse(saveProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());

            // Test3の解放記録を消し、ケースによっては研究4の完了も外す
            // Delete the Test3 unlock record and, depending on the case, also drop research 4's completion
            var itemInfos = (JArray)save["gameUnlockState"]["itemUnlockStateInfos"];
            itemInfos.First(info => Guid.Parse(info["guid"].Value<string>()) == Test3ItemGuid).Remove();
            if (!keepResearchCompleted)
            {
                var completed = (JArray)save["research"]["CompletedResearchGuids"];
                completed.First(entry => Guid.Parse(entry.Value<string>()) == Research4Guid).Remove();
            }

            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(save.ToString());

            var unlockState = loadProvider.GetService<IGameUnlockStateDataController>();
            var test3ItemId = MasterHolder.ItemMaster.GetItemId(Test3ItemGuid);
            Assert.AreEqual(expectedUnlocked, unlockState.ItemUnlockStateInfos[test3ItemId].IsUnlocked);
        }
    }
}
