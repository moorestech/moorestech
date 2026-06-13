using System.Collections;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Tests.EditModeInPlayingTest.Util;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
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
    public class MachineModuleSlotUITest
    {
        private const string MachineBlockName = "釜";
        private const string ModuleItemName = "歯車";
        private const string UiAddress = "Vanilla/UI/Block/MachineBlockInventory";

        // 釜のスロット構成（EditModeInPlayingTestModのblocks.jsonに対応）
        // Slot layout of the kiln machine (matches blocks.json in EditModeInPlayingTestMod)
        private const int InputSlotCount = 3;
        private const int OutputSlotCount = 1;
        private const int ModuleSlotCount = 4;
        private const int ModuleRangeStart = InputSlotCount + OutputSlotCount;

        [UnityTest]
        public IEnumerator ModuleSlotRenderAndEquipRoundTrip()
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
                BlockGameObject blockGameObject = null;
                for (var i = 0; i < 60 && blockGameObject == null; i++)
                {
                    ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out blockGameObject);
                    await UniTask.Yield();
                }
                Assert.IsNotNull(blockGameObject, "client BlockGameObject not spawned");

                // 機械インベントリUIをDI経由で生成し、モジュールスロット列が描画されることを確認
                // Instantiate the machine inventory UI via DI and verify the module slot row is rendered.
                using var loaded = await AddressableLoader.LoadAsync<GameObject>(UiAddress, CancellationToken.None);
                var instance = ClientDIContext.DIContainer.Instantiate(loaded.Asset);
                var view = instance.GetComponent<MachineBlockInventoryView>();
                view.Initialize(blockGameObject);
                Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, view.SubInventorySlotObjects.Count, "unified slot count mismatch");

                // モジュールスロットコンテナの下に4スロットが並んでいることを確認
                // Verify four slots are laid out under the module slot container.
                var moduleParent = FindChildRecursive(instance.transform, "ModuleSlots");
                Assert.IsNotNull(moduleParent, "ModuleSlots container not found in prefab");
                Assert.AreEqual(ModuleSlotCount, moduleParent.childCount, "module slot view count mismatch");

                // モジュールアイテムをプレイヤーへ付与し、付与先のメインスロット番号を特定する
                // Give the player a module item and locate the main inventory slot it landed in.
                await GiveItem(ModuleItemName, 1);
                var moduleItemId = FindItemId(ModuleItemName);
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                var playerInventory = await ClientContext.VanillaApi.Response.GetMyPlayerInventory(CancellationToken.None);
                var mainSlot = playerInventory.MainInventory.FindIndex(item => item.Id == moduleItemId);
                Assert.GreaterOrEqual(mainSlot, 0, "module item not found in player main inventory");

                // 既存の移動プロトコル（InventoryType.Block＋スロット番号）でモジュールスロットへ装着する
                // Equip into the module slot via the existing move protocol (InventoryType.Block + slot number).
                ClientContext.VanillaApi.SendOnly.ItemMove(1, ItemMoveType.SwapSlot,
                    InventoryIdentifierMessagePack.CreateMainMessage(playerId), mainSlot,
                    InventoryIdentifierMessagePack.CreateBlockMessage(pos), ModuleRangeStart);

                // 既存のインベントリ取得プロトコルへ装着が反映されるまでポーリングして確認
                // Poll the existing inventory request protocol until the equip is reflected.
                var inventoryResponse = await ClientContext.VanillaApi.Response.GetInventory(InventoryIdentifierMessagePack.CreateBlockMessage(pos), CancellationToken.None);
                for (var i = 0; i < 30 && inventoryResponse.Items[ModuleRangeStart].Id != moduleItemId; i++)
                {
                    await UniTask.Delay(100);
                    inventoryResponse = await ClientContext.VanillaApi.Response.GetInventory(InventoryIdentifierMessagePack.CreateBlockMessage(pos), CancellationToken.None);
                }
                Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, inventoryResponse.Items.Count, "request protocol slot count mismatch");
                Assert.AreEqual(moduleItemId, inventoryResponse.Items[ModuleRangeStart].Id, "module not equipped into the module slot");
                Assert.AreEqual(1, inventoryResponse.Items[ModuleRangeStart].Count);

                // UIを閉じて開き直しても装着が保持されていることを確認
                // Close and reopen the UI and verify the equip persists.
                Object.Destroy(instance);
                await UniTask.Yield();
                var reopened = ClientDIContext.DIContainer.Instantiate(loaded.Asset);
                var reopenedView = reopened.GetComponent<MachineBlockInventoryView>();
                reopenedView.Initialize(blockGameObject);
                Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, reopenedView.SubInventorySlotObjects.Count);

                var reopenedResponse = await ClientContext.VanillaApi.Response.GetInventory(InventoryIdentifierMessagePack.CreateBlockMessage(pos), CancellationToken.None);
                Assert.AreEqual(moduleItemId, reopenedResponse.Items[ModuleRangeStart].Id, "module lost after UI reopen");

                Object.Destroy(reopened);
            }

            // 名前からアイテムIDを引く
            // Look up an item id by name.
            ItemId FindItemId(string itemName)
            {
                foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
                {
                    if (MasterHolder.ItemMaster.GetItemMaster(itemId).Name == itemName) return itemId;
                }
                Assert.Fail($"Item not found: {itemName}");
                return new ItemId(-1);
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

            #endregion
        }
    }
}
