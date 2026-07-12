using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Tests.EditModeInPlayingTest.Util;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
    public class MachineRecipeSelectionUITest
    {
        private const string MachineBlockName = "釜";
        private const string UiAddress = "Vanilla/UI/Block/MachineBlockInventory";

        [UnityTest]
        public IEnumerator RecipeSlotsRenderAndHighlightSelectedRecipe()
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

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // 機械を設置し、クライアント側のBlockGameObjectがスポーンするまで待機
                // Place the machine and wait until the client-side BlockGameObject spawns.
                var pos = new Vector3Int(0, 0, 0);
                PlaceBlock(MachineBlockName, pos, BlockDirection.North);
                var blockGameObject = await WaitBlockGameObjectSpawn(pos);

                // 機械インベントリUIをDI経由で生成し、レシピスロット列が描画されることを確認
                // Instantiate the machine inventory UI via DI and verify the recipe slot row is rendered.
                using var loaded = await AddressableLoader.LoadAsync<GameObject>(UiAddress, CancellationToken.None);
                var instance = ClientDIContext.DIContainer.Instantiate(loaded.Asset);
                var view = instance.GetComponent<MachineBlockInventoryView>();
                view.Initialize(blockGameObject);

                var recipeSlotsParent = FindChildRecursive(instance.transform, "RecipeSlots");
                Assert.IsNotNull(recipeSlotsParent, "RecipeSlots container not found in prefab");

                // マスタから対象ブロックのレシピ一覧を導出し、アンロック済みレシピ数とスロット数が一致することを確認
                // Derive the block's recipe list from the master and verify slot count matches the unlocked recipe count.
                var blockGuid = blockGameObject.BlockMasterElement.BlockGuid;
                var blockRecipes = new List<MachineRecipeMasterElement>();
                foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
                {
                    if (recipe.BlockGuid == blockGuid) blockRecipes.Add(recipe);
                }
                Assert.AreEqual(2, blockRecipes.Count, "test master data recipe count mismatch");

                var slotViews = recipeSlotsParent.GetComponentsInChildren<ItemSlotView>();
                Assert.AreEqual(blockRecipes.Count, slotViews.Length, "recipe slot view count mismatch");

                // パネルのUpdate()が最低1回実行されるまで待ってから初期状態を確認する
                // Wait until the panel's Update() has run at least once before checking the initial state.
                await UniTask.DelayFrame(2);
                foreach (var slot in slotViews) Assert.IsFalse(IsHotBarSelected(slot), "slot highlighted before any recipe is selected");

                // サーバー側で1件目のレシピを選択し、対応するスロットのみハイライトされることを確認
                // Select the first recipe on the server and verify only its slot gets highlighted.
                var targetRecipe = blockRecipes[0];
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(pos, targetRecipe.MachineRecipeGuid, playerId);
                var response = await ClientContext.VanillaApi.Response.SendMachineRecipeSelectionRequest(request, CancellationToken.None);
                Assert.IsTrue(response.Success, $"recipe selection failed: {response.FailureReason}");

                // ブロック状態同期への反映とパネルのUpdate()によるハイライト反映を待つ
                // Wait for the block state sync and the panel's Update() to reflect the highlight.
                var highlighted = false;
                for (var i = 0; i < 50 && !highlighted; i++)
                {
                    await UniTask.Delay(100);
                    highlighted = IsHotBarSelected(slotViews[0]);
                }
                Assert.IsTrue(highlighted, "selected recipe slot was not highlighted");
                Assert.IsFalse(IsHotBarSelected(slotViews[1]), "unselected recipe slot was highlighted");

                Object.Destroy(instance);
            }

            // 名前で子Transformを再帰検索する
            // Recursively find a child transform by name.
            Transform FindChildRecursive(Transform parent, string childName)
            {
                if (parent.name == childName) return parent;
                foreach (Transform child in parent)
                {
                    var found = FindChildRecursive(child, childName);
                    if (found != null) return found;
                }
                return null;
            }

            // privateフィールドをリフレクションで辿りハイライト表示状態を取得する
            // Walk private fields via reflection to read the highlight display state.
            bool IsHotBarSelected(ItemSlotView slot)
            {
                var commonSlotViewField = typeof(ItemSlotView).GetField("commonSlotView", BindingFlags.NonPublic | BindingFlags.Instance);
                var commonSlotView = (CommonSlotView)commonSlotViewField.GetValue(slot);
                var hotBarSelectField = typeof(CommonSlotView).GetField("hotBarSelect", BindingFlags.NonPublic | BindingFlags.Instance);
                var hotBarSelectObject = (GameObject)hotBarSelectField.GetValue(commonSlotView);
                return hotBarSelectObject.activeSelf;
            }

            #endregion
        }
    }
}
