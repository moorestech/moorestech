// 削除ツールの新方式: なぞり選択は離しても残り、F長押し(2秒)で確定。Shift+クリック2点の箱で範囲選択。ESCで全解除。Ctrl+Zで復元
// New delete-tool flow: stroke selection survives release, F hold (2s) confirms; Shift+click two-point box selects a range; ESC clears; Ctrl+Z restores
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("delete-tool-box-select-hold-confirm", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -2f));

    // 準備: 木のチェストを一列に3つ直置き
    // Setup: place three wooden chests in a row directly
    var cells = new[] { new Vector3Int(2, 32, 4), new Vector3Int(4, 32, 4), new Vector3Int(6, 32, 4) };
    foreach (var cell in cells) PlaytestBlockOps.PlaceBlockDirect("木のチェスト", cell, BlockDirection.North);
    foreach (var cell in cells) await p.WaitBlockGameObject(cell);
    await p.Screenshot("01-prepared");

    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);

    // Step 1: なぞって離しても消えない
    // Step 1: a stroke that is released does not delete
    p.Note("Step 1: チェスト1をなぞって離す→赤いまま残り、消えない");
    await AimAtCell(cells[0]);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(10); SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(1.0f);
    p.Assert(p.GetBlock(cells[0]) != null, "Step1: 離しただけでは撤去されない");
    p.Assert(TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 1, $"Step1: ツールチップに選択件数1が出る 実際:{TooltipText()}");
    await p.Screenshot("02-stroke-kept");

    // Step 2: F長押しで確定
    // Step 2: F hold confirms
    p.Note("Step 2: F長押し2秒で選択を削除");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds * 0.5f);
    await p.Screenshot("03-holding");
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds * 0.5f + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[0]) == null, 15f, "Step2: F長押し満了でチェスト1が撤去される");
    await p.Screenshot("04-deleted");

    // Step 3: Shift+クリック2点の箱でチェスト2・3を選ぶ（終点確定だけでは消えない）
    // Step 3: box-select chests 2 and 3 with Shift+click then click; confirming the end alone deletes nothing
    p.Note("Step 3: Shift+クリックで始点、クリックで終点→範囲内2件");
    await BoxSelect(cells[1] + new Vector3Int(-1, 0, -1), cells[2] + new Vector3Int(1, 0, 1), 2, "Step3");
    p.Assert(p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, "Step3: 終点確定だけでは撤去されない");
    await p.Screenshot("06-box-selected");

    // Step 4: ESCで全解除→再び箱→F長押し
    // Step 4: ESC clears, box again, then F hold
    p.Note("Step 4: ESCで全解除（赤が戻る）→もう一度箱で選び直しF長押し");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.DeleteBar, "Step4: 選択があるときのESCは破壊モードに留まる");
    p.Assert(TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 0, $"Step4: 全解除で件数行が消える 実際:{TooltipText()}");
    await BoxSelect(cells[1] + new Vector3Int(-1, 0, -1), cells[2] + new Vector3Int(1, 0, 1), 2, "Step4");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[1]) == null && p.GetBlock(cells[2]) == null, 15f, "Step4: 箱の2件がまとめて撤去される");
    await p.Screenshot("07-box-deleted");

    // Step 5: Ctrl+Zで2件が戻る
    // Step 5: Ctrl+Z restores both
    p.Note("Step 5: Ctrl+Zで箱の2件が1バッチで戻る");
    foreach (var (key, down) in new[] { (Key.LeftCtrl, true), (Key.Z, true), (Key.Z, false), (Key.LeftCtrl, false) })
    {
        if (down) SemanticInput.KeyDown(key);
        else SemanticInput.KeyUp(key);
        await UniTask.DelayFrame(3);
    }
    await p.Until(() => p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, 15f, "Step5: Ctrl+Zで2件とも復元される");
    await p.Screenshot("08-undone");

    #region Internal

    MouseCursorTooltipState Tooltip() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<MouseCursorTooltipState>();

    string TooltipText()
    {
        var presentation = Tooltip().GetPresentation();
        return string.Join("|", presentation.Lines.Select(line => line.Key.Key + ":" + string.Join(",", line.TextParams ?? System.Array.Empty<string>())));
    }

    // 指定キーの行の第1引数を整数で読む（行が無ければ 0）
    // Read the first param of the line with the given key as an int (0 when absent)
    int TooltipParam(LocalizationKey key)
    {
        var line = Tooltip().GetPresentation().Lines.FirstOrDefault(l => l.Key.Key == key.Key);
        return line.TextParams != null && line.TextParams.Count > 0 && int.TryParse(line.TextParams[0], out var n) ? n : 0;
    }

    // Shift+クリックで始点、クリックで終点。終点待ち中の件数が expectedCount になることを確かめる
    // Shift+click the start, click the end; assert the pending count equals expectedCount
    async UniTask BoxSelect(Vector3Int startCell, Vector3Int endCell, int expectedCount, string label)
    {
        await AimAtGround(startCell);
        SemanticInput.KeyDown(Key.LeftShift);
        await UniTask.DelayFrame(3);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(5);
        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.LeftShift);
        await p.Until(() => GameObject.Find("DeleteBoxStartMarker") != null && GameObject.Find("DeleteBoxStartMarker").activeSelf, 5f, $"{label}: 始点マーカーが出る");
        await AimAtGround(endCell);
        await p.Until(() => TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange) == expectedCount, 5f, $"{label}: 範囲内の対象{expectedCount}件 実際:{TooltipText()}");
        await p.Screenshot($"05-{label}-box-end-hover");
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(5);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.5f);
    }

    // ブロックの各コライダー中心のうち削除ツールの実レイキャストが当該ブロックを返す点を照準する
    // Aim at the collider center where the real delete-tool raycast returns that block
    async UniTask AimAtCell(Vector3Int pos)
    {
        var block = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        foreach (var point in block.GetComponentsInChildren<Collider>().Select(c => c.bounds.center).OrderBy(c => Vector3.Distance(c, Camera.main.transform.position)).ToList())
        {
            await p.AimAt(point);
            var target = Client.Game.InGame.Control.DeleteTargetRaycaster.AimAt(DeleteAimFilter.Frontmost).Target;
            if (target is Client.Game.InGame.Block.BlockGameObjectChild child && ReferenceEquals(child.BlockGameObject, block)) return;
        }
        p.Assert(false, $"AimAtCell: {pos} を照準できない");
    }

    // 地面セルの上面を照準する。1x1設置と同じ解決で上のセルが選ばれる
    // Aim at the top face of a ground cell; the 1x1 placement rule resolves to the cell above it
    async UniTask AimAtGround(Vector3Int cellAboveGround)
    {
        await p.AimAt(new Vector3(cellAboveGround.x + 0.5f, cellAboveGround.y - 0.02f, cellAboveGround.z + 0.5f));
    }

    #endregion
});
