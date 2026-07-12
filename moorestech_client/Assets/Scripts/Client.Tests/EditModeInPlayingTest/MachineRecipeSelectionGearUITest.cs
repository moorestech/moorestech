using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Tests.EditModeInPlayingTest.Util;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using static Client.Tests.EditModeInPlayingTest.MachineRecipeSelectionTestHelper;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// GearMachineBlockInventoryプレハブを実際にインスタンス化し、レシピ選択パネルの配線が
    /// 生きていること（スロット生成）を検証する。ハイライト検証は継承元のElectric側
    /// (MachineRecipeSelectionUITest)で担保済みのため、ここではスロット数のみ確認する。
    /// Instantiates the GearMachineBlockInventory prefab to verify the recipe panel wiring
    /// survives (slot generation); highlight behavior is already covered by the Electric-side test.
    /// </summary>
    public class MachineRecipeSelectionGearUITest
    {
        private const string GearBlockName = "ふるい";
        private const string GearUiAddress = "Vanilla/UI/Block/GearMachineBlockInventory";

        [UnityTest]
        public IEnumerator RecipeSlotsRenderMatchesUnlockedRecipeCount()
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

                // 歯車機械を設置し、クライアント側のBlockGameObjectがスポーンするまで待機
                // Place the gear machine and wait until the client-side BlockGameObject spawns.
                var pos = new Vector3Int(0, 0, 0);
                PlaceBlock(GearBlockName, pos, BlockDirection.North);
                var blockGameObject = await WaitBlockGameObjectSpawn(pos);

                // GearMachineBlockInventoryのUIをDI経由で生成し、配線が生きていることを確認
                // Instantiate the GearMachineBlockInventory UI via DI and verify the wiring survives.
                using var loaded = await AddressableLoader.LoadAsync<GameObject>(GearUiAddress, CancellationToken.None);
                var instance = ClientDIContext.DIContainer.Instantiate(loaded.Asset);
                var view = instance.GetComponent<MachineBlockInventoryView>();
                view.Initialize(blockGameObject);

                var recipeSlotsParent = FindChildRecursive(instance.transform, "RecipeSlots");
                Assert.IsNotNull(recipeSlotsParent, "RecipeSlots container not found in Gear prefab");

                // マスタから対象ブロックのレシピ一覧を導出し、アンロック済みレシピ数とスロット数が一致することを確認
                // Derive the block's recipe list from the master and verify slot count matches the unlocked recipe count.
                var blockGuid = blockGameObject.BlockMasterElement.BlockGuid;
                var blockRecipes = new List<MachineRecipeMasterElement>();
                foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
                {
                    if (recipe.BlockGuid == blockGuid) blockRecipes.Add(recipe);
                }
                Assert.AreEqual(1, blockRecipes.Count, "test master data recipe count mismatch");

                var slotViews = recipeSlotsParent.GetComponentsInChildren<ItemSlotView>();
                Assert.AreEqual(blockRecipes.Count, slotViews.Length, "gear recipe slot view count mismatch");

                Object.Destroy(instance);
            }

            #endregion
        }
    }
}
