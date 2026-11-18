using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Server.Protocol.PacketResponse;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    /// <summary>
    ///     ベルトで届いた製作上限超えの素材で止まった理由が、実ゲームの起動経路で UITK のブロックの欄に載り、研究で消えることを確かめる
    ///     Verifies, through the real game boot, that the stall from an above-cap material arriving by belt reaches the UITK block context entry and clears after research
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class MachineInputLevelStallContextModelTest
    {
        private const string MachineBlockName = "石の粉砕機";
        private static readonly Guid CrusherInheritRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000e0de0002");
        private static readonly Guid MachineUpgradeResearchGuid = Guid.Parse("e0de0000-0000-4000-8000-000000000001");
        private static readonly Guid SourceLv2ItemGuid = Guid.Parse("00000000-0000-0000-e0de-000000000012");

        [UnityTest]
        public IEnumerator 製作上限を超える素材がベルトで届くと止まった理由がブロックの欄のモデルに載り研究で消える()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト後にデバッグ無効化を解除
            // Clear debug disabling after the test
#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // 粉砕機を手の届く所に置いてFで開く（ブロックの欄は本番のUI遷移で開いたときだけ追う）
                // Place the crusher within reach and open it with F (the block context entry is only tracked once opened through the production UI transition)
                var machineObject = await InteractKeyBlockOpener.PlaceNearPlayerAndOpen(MachineBlockName);
                var machinePos = machineObject.BlockPosInfo.OriginalPos;
                var machine = ServerContext.WorldBlockDatastore.GetBlock(machinePos);

                // 上限つき継承レシピを通常のUI経路で選ぶ
                // Select the capped inheritance recipe through the production UI path
                await MachineRecipeSelectAwaiter.SelectAndWait(CrusherInheritRecipeGuid);

                // 製作上限 Lv1 の間に Lv2 の素材をチェストへ入れると、ベルトの先頭で止まり、理由が欄に載る
                // Putting a Lv2 material in the chest under the Lv1 craft cap stalls it at the belt head and the reason reaches the entry
                var sourceLv2 = MasterHolder.ItemMaster.GetItemId(SourceLv2ItemGuid);
                var feed = new MachineInputStallFeedSetupStep(machinePos, sourceLv2);
                await feed.RunOnServerAsync();
                Assert.IsNotNull(feed.Chest, "the feed chest was not placed");
                Assert.IsNotNull(feed.Belt, "the feed belt was not placed");
                Assert.AreEqual(0, feed.RemainingSourceCount, "the Lv2 source did not fit in the chest");
                var stall = await WaitUntilStallAppears();
                Assert.AreEqual(MachineInputStallReasonKind.AboveCraftCap, stall.Reason);
                Assert.AreEqual(sourceLv2, stall.WaitingItemId);

                // 研究で製作上限を2へ上げると、待っていた Lv2 が入り、理由が欄から消える
                // Raising the craft cap to 2 by research lets the waiting Lv2 in and the reason leaves the entry
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                var research = new MachineInputStallResearchStep(MachineUpgradeResearchGuid, playerId);
                await research.RunOnServerAsync();
                Assert.IsTrue(research.Completed, "research did not complete");
                await WaitUntilStallCleared();

                // 加工後の継承出力IDと個数を検証
                // Verify the inherited output ID and count after processing
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(CrusherInheritRecipeGuid);
                var processor = machine.GetComponent<VanillaMachineProcessorComponent>();
                var inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
                var outputSlot = ((Mooresmaster.Model.BlocksModule.IMachineParam)machine.BlockMasterElement.BlockParam).InputSlotCount;
                var outputId = MachineRecipeLevelInheritanceUtil.ResolveOutputItemId(recipe, 0, 2);
                for (var frame = 0; frame < 3600 && inventory.GetItem(outputSlot).Count == 0; frame++)
                {
                    processor.SupplyExternalPower(processor.EffectiveRequestPower);
                    await UniTask.Yield();
                }
                var output = inventory.GetItem(outputSlot);
                Assert.AreEqual(outputId, output.Id);
                Assert.AreEqual(recipe.OutputItems[0].Count, output.Count);
                Assert.AreEqual(ItemMaster.EmptyItemId, feed.Chest.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Id, "the Lv2 is still in the chest");

                await InteractKeyBlockOpener.CloseToGameScreen();
            }

            // 開いているブロックの搬入停止の欄を UITK のモデルから読む
            // Read the open block's input-stall entry from the UITK model
            SectionContent<MachineInputStall> StallEntry()
            {
                return ClientDIContext.DIContainer.DIContainerResolver.Resolve<IBlockContextModel>().Current.Value.MachineInputStall;
            }

            // ベルトの搬送と1秒ごとの問い合わせを待ち、取得済みの止まった理由が欄に載るまで待つ
            // Wait for the belt transport and the 1 s query until a fetched stall reaches the entry
            async UniTask<MachineInputStall> WaitUntilStallAppears()
            {
                var firstTick = Core.Update.GameUpdater.CurrentTick;
                for (var i = 0; i < 100 && !StallEntry().IsPresent; i++) await UniTask.Delay(100);
                var entry = StallEntry();
                Assert.IsTrue(entry.TryGetPresent(out var stall), $"a fetched machine input stall never reached the block context entry; ticks: {firstTick}->{Core.Update.GameUpdater.CurrentTick}; latest: {Describe(entry)}");
                return stall;
            }

            // 止まった理由が欄から消えるまで待つ（取得済みで止まっていなければ欄ごと無い）
            // Wait until the stall leaves the entry; once fetched and not stalled, the entry is absent
            async UniTask WaitUntilStallCleared()
            {
                for (var i = 0; i < 50 && !StallEntry().IsAbsent; i++) await UniTask.Delay(100);
                var entry = StallEntry();
                Assert.IsTrue(entry.IsAbsent, $"the machine input stall did not clear after the craft cap was raised; latest: {Describe(entry)}");
            }

            // 失敗の文に欄の状態（取得不可なら取得状態）を出す
            // Describe the entry's state (the fetch state when unavailable) for failure messages
            string Describe(SectionContent<MachineInputStall> entry)
            {
                if (entry.IsAbsent) return "absent";
                if (entry.TryGetUnavailable(out var fetchState)) return $"unavailable({fetchState})";
                entry.TryGetPresent(out var stall);
                return $"present(reason={stall.Reason} item={stall.WaitingItemId})";
            }

            #endregion
        }
    }
}
