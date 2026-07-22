// BP右クリック削除E2E検証(UI経路): BPをコピー作成→ビルドメニューのBPスロットを右クリックで削除する
// 検証項目: BP作成とメニュー表示、右クリックでサーバーのBPデータストアから削除、メニューからスロット消滅
// Blueprint right-click delete E2E (UI route): create a blueprint via copy, then right-click its menu slot to delete it.
// Verifies: creation + menu listing, right-click removes it from the server datastore, and the slot disappears from the menu.
using System.Linq;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-right-click-delete-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(3f, 33.5f, 3f));
    await p.WaitSeconds(0.5f);

    // コピー元ブロックを1つ直設置する
    // Place a single source block directly
    var chestPos = new Vector3Int(2, 32, 2);
    p.PlaceBlockDirect("木のチェスト", chestPos, BlockDirection.North);
    await p.WaitBlockGameObject(chestPos);

    // BPコピーツールを選択しドラッグでBP「delbp」を作成する
    // Select the copy tool and create blueprint "delbp" via drag
    await OpenBuildMenuAndClickTextSlot("ブループリントコピー", "01-menu-copy-tool");

    await p.AimAt(new Vector3(1.5f, 32f, 1.5f));
    SemanticInput.MouseButtonDown(0);
    await UniTask.DelayFrame(3);
    await p.AimAt(new Vector3(3.5f, 32f, 3.5f));
    await UniTask.DelayFrame(3);
    SemanticInput.MouseButtonUp(0);

    // 名前入力ダイアログで命名して確定する
    // Name the blueprint in the dialog and confirm
    var nameInputView = UnityEngine.Object.FindFirstObjectByType<BlueprintNameInputView>(FindObjectsInactive.Include);
    await p.Until(() => nameInputView.gameObject.activeSelf, 10f, "ドラッグ解放で名前入力ダイアログが開く");
    var nameFieldInfo = typeof(BlueprintNameInputView).GetField("nameInputField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var inputField = (TMPro.TMP_InputField)nameFieldInfo.GetValue(nameInputView);
    inputField.text = "delbp";
    var confirmInfo = typeof(BlueprintNameInputView).GetField("confirmButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var confirmButton = (UnityEngine.UI.Button)confirmInfo.GetValue(nameInputView);
    ClickUi(confirmButton.gameObject, PointerEventData.InputButton.Left, true);

    // サーバー登録を確認する
    // Confirm server-side registration
    var datastore = p.ServerService<IBlueprintDatastore>();
    await p.Until(() => datastore.Blueprints.Any(b => b.Name == "delbp"), 15f, "サーバーにBP『delbp』が登録される");

    // メニューを開きBPスロットの表示を確認する
    // Open the menu and confirm the blueprint slot is listed
    await p.ExitToGameScreen();
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.Until(() => FindTextSlot("delbp") != null, 15f, "ビルドメニューにBP『delbp』が表示される");
    await p.Screenshot("02-menu-bp-entry");

    // BPスロットを右クリックし、サーバー削除とスロット消滅を検証する
    // Right-click the blueprint slot, then verify server-side deletion and slot removal
    var slot = FindTextSlot("delbp");
    ClickUi(slot.GetComponentInChildren<CommonSlotView>(true).gameObject, PointerEventData.InputButton.Right, false);
    await p.Until(() => !datastore.Blueprints.Any(b => b.Name == "delbp"), 15f, "右クリックでサーバーからBPが削除される");
    await p.Until(() => FindTextSlot("delbp") == null, 15f, "メニューからBPスロットが消滅する");
    await p.Screenshot("03-menu-after-delete");
    await p.ExitToGameScreen();

    #region Internal

    async UniTask OpenBuildMenuAndClickTextSlot(string label, string screenshotName)
    {
        // PlaceBlock中はTab、それ以外はBで開く（実プレイと同じキー割当）
        // Open with Tab while in PlaceBlock, otherwise with B (same bindings as real play)
        for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
        {
            var openKey = p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B;
            await p.PressKey(openKey);
            var openDeadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < openDeadline && p.CurrentUiState != UIStateEnum.BuildMenu) await UniTask.DelayFrame(5);
        }
        p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"ビルドメニューが開く ({label})");

        // 非同期再構築がクリックを破棄するレースがあるため、遷移するまでクリックを繰り返す
        // The async rebuild can wipe a pending click, so retry clicking until the transition happens
        var screenshotTaken = false;
        var clickDeadline = Time.realtimeSinceStartup + 20f;
        while (p.CurrentUiState != UIStateEnum.PlaceBlock && Time.realtimeSinceStartup < clickDeadline)
        {
            var slotView = FindTextSlot(label);
            if (slotView != null && !screenshotTaken && screenshotName != null)
            {
                await p.Screenshot(screenshotName);
                screenshotTaken = true;
                slotView = FindTextSlot(label);
            }
            if (slotView != null) ClickUi(slotView.GetComponentInChildren<CommonSlotView>(true).gameObject, PointerEventData.InputButton.Left, true);
            await UniTask.DelayFrame(10);
        }
        p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"スロット『{label}』選択でPlaceBlockへ遷移");
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.6f));
    }

    ItemSlotView FindTextSlot(string label)
    {
        // アイコン無し（ItemViewData==null）スロットを表示テキストで特定する
        // Locate icon-less slots (ItemViewData==null) by display text
        var buildMenuView = UnityEngine.Object.FindFirstObjectByType<BuildMenuView>(FindObjectsInactive.Include);
        if (buildMenuView == null || !buildMenuView.gameObject.activeInHierarchy) return null;
        foreach (var slotView in buildMenuView.GetComponentsInChildren<ItemSlotView>(true))
        {
            if (slotView.ItemViewData != null) continue;
            if (slotView.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == label)) return slotView;
        }
        return null;
    }

    void ClickUi(GameObject target, PointerEventData.InputButton button, bool withClickHandler)
    {
        // EventSystem直叩き（OSカーソル非依存）。右クリック削除はOnPointerUpのRight判定で発火する
        // Direct EventSystem execution (OS-cursor independent); right-click delete fires on the Right-button pointerUp
        var eventData = new PointerEventData(EventSystem.current) { button = button };
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerUpHandler);
        if (withClickHandler) ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
    }

    #endregion
});
