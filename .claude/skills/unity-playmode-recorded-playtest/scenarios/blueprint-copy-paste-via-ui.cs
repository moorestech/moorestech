// BP統合検証(UI経路)
// コピー(ドラッグ+スクロール+名前入力)→R回転貼付→セーブロード往復
// コピー元:
// ・チェスト(2,32,2)North
// ・石窯(4,32,2)East
// ・チェスト(2,32,4)North
// ・ボックス(0,32,0)-(8,38,6)
// アンカー(4,32,3)基準のオフセットを厳密assert
// 回転1回・貼付アンカー(14,32,14)の期待位置も検証
// Blueprint integration scenario (UI route): copy via XZ drag + scroll height + naming, rotated paste, save/load round-trip
// Sources: chest(2,32,2)North / stone kiln(4,32,2)East / chest(2,32,4)North, box (0,32,0)-(8,38,6)
// Asserts exact offsets from anchor (4,32,3) and expected positions for one R rotation pasted at anchor (14,32,14)
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Blueprint;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-copy-paste-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(5f, 33.5f, 4f));

    // 解放と建設コスト付与（内訳はコード参照）
    // Unlock and grant construction costs (chest: 1 UI place + 2 paste, kiln: 1 paste)
    await p.PrepareBlockForUiPlacement("木のチェスト", 4);
    await p.PrepareBlockForUiPlacement("石窯", 2);

    // コピー元: UI設置1+直設置2
    // Source blocks: one via UI, two direct (one with a non-default direction)
    await p.PlaceBlockViaUi("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    p.PlaceBlockDirect("石窯", new Vector3Int(4, 32, 2), BlockDirection.East);
    p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 4), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(4, 32, 2));
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 4));
    await p.Screenshot("01-source-blocks");

    // BPコピーツールを選択（テキストスロット）
    // Select the blueprint copy tool (icon-less text slot)
    await OpenBuildMenuAndClickTextSlot("ブループリントコピー", "02-menu-copy-tool");

    var hotBarView = UnityEngine.Object.FindFirstObjectByType<HotBarView>();
    var hotbarBefore = hotBarView.SelectIndex;

    // XZドラッグ+スクロール+2で範囲選択
    // Build the selection box via XZ drag plus +2 scroll steps
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    SemanticInput.MouseButtonDown(0);
    await UniTask.DelayFrame(3);
    await p.AimAt(new Vector3(4.5f, 32f, 3.5f));
    await p.AimAt(new Vector3(8.5f, 32f, 6.5f));
    InjectScrollWithHeldLeft(200f);
    await UniTask.DelayFrame(4);

    // ボックス可視化: min(0,32,0)-max(8,38,6)→サイズ(9,7,7)
    // 両端とも石窯に遮られない地面セルを狙う
    // Box visualizer: min(0,32,0)-max(8,38,6) -> size (9,7,7); both corners aim at ground cells clear of the kiln
    var visualizer = GameObject.Find("BlueprintAreaVisualizer");
    p.Assert(visualizer != null && visualizer.activeSelf, "ドラッグ中に選択ボックスが表示される");
    p.Assert(visualizer != null && visualizer.transform.localScale == new Vector3(9f, 7f, 7f), $"スクロール+2で選択ボックスサイズが(9,7,7) 実際:{(visualizer != null ? visualizer.transform.localScale.ToString() : "null")}");
    await p.Screenshot("03-drag-box");

    SemanticInput.MouseButtonUp(0);

    // ドラッグ解放で名前入力ダイアログが開く
    // Releasing the drag opens the name input dialog
    var nameInputView = UnityEngine.Object.FindFirstObjectByType<BlueprintNameInputView>(FindObjectsInactive.Include);
    await p.Until(() => nameInputView.gameObject.activeSelf, 10f, "ドラッグ解放で名前入力ダイアログが開く");

    // ウォッチリスト2観察: ドラッグ中スクロールでホットバー選択が同時に動くか（観察のみ・失敗にしない）
    // Watch-list 2 observation: whether the drag scroll also moved the hotbar selection (observe only)
    var hotbarAfter = hotBarView.SelectIndex;
    p.Assert(true, $"watchlist2-observe: ドラッグ中スクロールでホットバー選択 {hotbarBefore} -> {hotbarAfter}");

    // 名前入力中のB/G/V/Tabキー抑止を検証
    // Key suppression while naming: injecting B/G/V/Tab must not leave PlaceBlock nor close the dialog
    var nameFieldInfo = typeof(BlueprintNameInputView).GetField("nameInputField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var inputField = (TMPro.TMP_InputField)nameFieldInfo.GetValue(nameInputView);
    if (!inputField.isFocused)
    {
        EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        inputField.ActivateInputField();
        await UniTask.DelayFrame(3);
    }
    p.Assert(inputField.isFocused, "名前入力フィールドがフォーカスされている");

    await p.PressKey(Key.B);
    await p.PressKey(Key.G);
    await p.PressKey(Key.V);
    await p.PressKey(Key.Tab);
    await UniTask.DelayFrame(3);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"B/G/V/Tab注入後もPlaceBlockのまま 実際:{p.CurrentUiState}");
    p.Assert(nameInputView.gameObject.activeSelf, "キー注入後もダイアログが開いたまま");
    await p.Screenshot("04-name-dialog");

    // 名前を設定して確定ボタンをクリック
    // Set the name and click the confirm button
    inputField.text = "conveyor";
    var confirmInfo = typeof(BlueprintNameInputView).GetField("confirmButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var confirmButton = (UnityEngine.UI.Button)confirmInfo.GetValue(nameInputView);
    ClickUi(confirmButton.gameObject);

    // サーバー登録とBPオフセット・向きを検証
    // Verify server registration and BP contents (offsets/directions relative to anchor (4,32,3))
    var datastore = p.ServerService<IBlueprintDatastore>();
    await p.Until(() => datastore.Blueprints.Any(b => b.Name == "conveyor"), 15f, "サーバーにBP『conveyor』が登録される");
    var bp = datastore.Blueprints.First(b => b.Name == "conveyor");
    p.Assert(bp.Blocks.Count == 3, $"BPのブロック数が3 実際:{bp.Blocks.Count}");
    p.Assert(HasBpBlock(bp, new Vector3Int(-2, 0, -1), BlockDirection.North), "BP内チェストA offset(-2,0,-1) North");
    p.Assert(HasBpBlock(bp, new Vector3Int(0, 0, -1), BlockDirection.East), "BP内石窯 offset(0,0,-1) East");
    p.Assert(HasBpBlock(bp, new Vector3Int(-2, 0, 1), BlockDirection.North), "BP内チェストB offset(-2,0,1) North");

    // 貼付:メニュー→BP選択→R回転→(14,32,14)
    // Paste: reopen menu, select the BP entry, rotate with R, paste at anchor (14,32,14)
    p.WarpPlayer(new Vector3(14f, 33.5f, 14f));
    await OpenBuildMenuAndClickTextSlot("conveyor", "05-menu-bp-entry");
    await p.PressKey(Key.R);
    await p.AimAt(new Vector3(14.5f, 32f, 14.5f));
    await UniTask.DelayFrame(3);
    await p.Screenshot("06-paste-preview");
    await p.ClickPlace();

    // 回転1回の期待位置:
    // ・チェストA(13,32,16)East
    // ・石窯(13,32,12)South
    // ・チェストB(15,32,16)East
    // Expected after one rotation: chestA (13,32,16) East / kiln (13,32,12) South / chestB (15,32,16) East
    var chestAPos = new Vector3Int(13, 32, 16);
    var kilnPos = new Vector3Int(13, 32, 12);
    var chestBPos = new Vector3Int(15, 32, 16);
    await p.Until(() => p.GetBlock(chestAPos) != null && p.GetBlock(kilnPos) != null && p.GetBlock(chestBPos) != null, 20f, "R回転貼り付けで3ブロックがサーバーに設置される");

    var chestId = PlaytestBlockOps.ResolveBlockId("木のチェスト");
    var kilnId = PlaytestBlockOps.ResolveBlockId("石窯");
    AssertPlaced(chestAPos, chestId, BlockDirection.East, "貼り付けチェストA");
    AssertPlaced(kilnPos, kilnId, BlockDirection.South, "貼り付け石窯");
    AssertPlaced(chestBPos, chestId, BlockDirection.East, "貼り付けチェストB");

    await p.WaitBlockGameObject(kilnPos);
    await p.ExitToGameScreen();
    await p.Screenshot("07-pasted");

    // セーブ→JSON検証→再ロード→メニュー確認
    // Save, verify the JSON, reload through the same path as WorldLoaderFromJson, confirm the menu entry survives
    var savePath = p.ServerService<SaveJsonFilePath>().Path;
    ClientContext.VanillaApi.SendOnly.Save();
    await p.Until(() => System.IO.File.Exists(savePath) && System.IO.File.ReadAllText(savePath).Contains("conveyor"), 30f, "セーブファイルにBPが書き出される");

    var loaded = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(System.IO.File.ReadAllText(savePath));
    p.Assert(loaded.Blueprints != null && loaded.Blueprints.Any(b => b.Name == "conveyor" && b.Blocks.Count == 3), "セーブJSONのblueprintsにconveyor(3ブロック)が含まれる");

    datastore.LoadBlueprints(new System.Collections.Generic.List<BlueprintJsonObject>());
    p.Assert(datastore.Blueprints.Count == 0, "再ロード前にBPデータストアを空にできる");
    datastore.LoadBlueprints(loaded.Blueprints);
    p.Assert(datastore.Blueprints.Any(b => b.Name == "conveyor"), "セーブJSONからBPデータストアへ復元される");

    // 復元後メニューにBPスロット表示
    // The rebuilt build menu still lists the blueprint slot
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.Until(() => FindTextSlot("conveyor") != null, 15f, "再ロード後のビルドメニューにBP『conveyor』が表示される");
    await p.Screenshot("08-menu-after-reload");
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

        // BPライブラリ更新の非同期再構築がクリックを破棄するレースがあるため、遷移するまでクリックを繰り返す
        // The async BP-library rebuild can wipe a pending click, so retry clicking until the transition happens
        var screenshotTaken = false;
        var clickDeadline = Time.realtimeSinceStartup + 20f;
        while (p.CurrentUiState != UIStateEnum.PlaceBlock && Time.realtimeSinceStartup < clickDeadline)
        {
            var slot = FindTextSlot(label);
            if (slot != null && !screenshotTaken && screenshotName != null)
            {
                await p.Screenshot(screenshotName);
                screenshotTaken = true;

                // スクショのawait中に非同期再構築でスロットが破棄され得るため取り直す
                // The async rebuild may destroy the slot during the screenshot await, so re-fetch it
                slot = FindTextSlot(label);
            }
            if (slot != null) ClickUi(slot.GetComponentInChildren<CommonSlotView>(true).gameObject);
            await UniTask.DelayFrame(10);
        }
        p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"スロット『{label}』選択でPlaceBlockへ遷移");

        // PlaceBlock遷移直後のカメラtweenが落ち着くまで待つ
        // Wait for the camera tween right after entering PlaceBlock to settle
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.6f));
    }

    ItemSlotView FindTextSlot(string label)
    {
        // アイコン無し（ItemViewData==null）スロットを表示テキストで特定する（閉じたメニューの残骸スロットは対象外）
        // Locate icon-less slots (ItemViewData==null) by display text; skip stale slots under a closed menu
        var buildMenuView = UnityEngine.Object.FindFirstObjectByType<BuildMenuView>(FindObjectsInactive.Include);
        if (buildMenuView == null || !buildMenuView.gameObject.activeInHierarchy) return null;
        foreach (var slot in buildMenuView.GetComponentsInChildren<ItemSlotView>(true))
        {
            if (slot.ItemViewData != null) continue;
            if (slot.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == label)) return slot;
        }
        return null;
    }

    void ClickUi(GameObject target)
    {
        // EventSystem直叩き（OSカーソル非依存）。スロットはDown/Up、ボタンはClickで発火する
        // Direct EventSystem execution (OS-cursor independent); slots fire on Down/Up, buttons on Click
        var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
    }

    void InjectScrollWithHeldLeft(float scrollY)
    {
        // ドラッグ保持中のスクロール注入。held状態と同座標を同時にre-queueして誤エッジを防ぐ
        // Inject scroll while dragging; re-queue the held button and same position to avoid spurious edges
        var mouse = Mouse.current;
        var state = new MouseState
        {
            position = mouse.position.ReadValue(),
            delta = Vector2.zero,
            scroll = new Vector2(0f, scrollY),
        };
        state = state.WithButton(MouseButton.Left, true);
        InputSystem.QueueStateEvent(mouse, state);
    }

    bool HasBpBlock(BlueprintJsonObject blueprint, Vector3Int offset, BlockDirection direction)
    {
        return blueprint.Blocks.Any(b => b.Offset == offset && b.Direction == (int)direction);
    }

    void AssertPlaced(Vector3Int pos, BlockId expectedId, BlockDirection expectedDir, string label)
    {
        var block = p.GetBlock(pos);
        var ok = block != null && block.BlockId == expectedId && block.BlockPositionInfo.BlockDirection == expectedDir && block.BlockPositionInfo.OriginalPos == pos;
        var actual = block == null ? "null" : $"id={block.BlockId} dir={block.BlockPositionInfo.BlockDirection} origin={block.BlockPositionInfo.OriginalPos}";
        p.Assert(ok, $"{label}: {pos} {expectedDir} (実際: {actual})");
    }

    #endregion
});
