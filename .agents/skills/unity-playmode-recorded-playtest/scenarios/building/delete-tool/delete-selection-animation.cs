// 実設置アニメーション中の選択と取消を、実Rendererと通知で観測する
// Observe selection and cancellation during real placement animation through renderers and events
using System.Linq;
using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using static Client.Playtest.Operations.Ui.DeleteBoxScenarioInput;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

return PlaytestRunner.Run("delete-selection-animation", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -2f));
    foreach (var cancelEarly in new[] { false, true })
    {
        var cell = new Vector3Int(cancelEarly ? 6 : 2, 32, 4);
        p.WarpPlayer(new Vector3(cell.x + 0.5f, 33.5f, cell.z - 6f));
        await p.WaitSeconds(0.5f);
        p.Assert(p.GetBlock(cell) == null, $"fixture空き:{cell}");
        p.Note(cancelEarly ? "設置アニメ中に選択取消し、完了後に赤が復活しない対照" : "設置アニメ中に選択し、完了後も本体の赤を保持");
        await p.PlaceBlockViaUi("木のチェスト", cell, BlockDirection.North);
        await p.WaitBlockGameObject(cell);
        // アニメ中の本体だけを追跡し、補助表示の赤を数えない
        // Track animated body renderers without counting red helper visuals
        var owner = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(cell);
        var finished = false;
        var start = Time.time;
        var finishedAt = -1f;
        using var subscription = owner.OnFinishedPlaceAnimation.Subscribe(_ => { finished = true; finishedAt = Time.time; });
        var animationShader = MaterialConst.GetPlaceBlockAnimationMaterial().shader;
        var renderers = owner.GetComponentsInChildren<MeshRenderer>().Where(r => r.sharedMaterials.Any(m => m.shader == animationShader)).ToArray();
        p.Assert(renderers.Length > 0 && !finished, "実本体Rendererが設置アニメ中である");
        await p.PressKey(Key.G);
        await p.WaitUiState(UIStateEnum.DeleteBar, 5f);
        // 時間窓内の操作でも各入力後0.5秒を保ち、カメラ静定済みの本体へ直接照準する
        // Keep half-second input pacing inside the animation window and aim at the settled body collider
        var clickCollider = owner.GetComponentInChildren<BlockGameObjectChild>().GetComponent<Collider>();
        SemanticInput.MouseMoveTo(Camera.main.WorldToScreenPoint(clickCollider.bounds.center));
        await p.WaitSeconds(0.5f);
        p.Assert(DeleteTargetRaycaster.AimAt(DeleteAimFilter.Frontmost).Target is BlockGameObjectChild aimed && ReferenceEquals(aimed.BlockGameObject, owner), "実レイが設置中の本体を拾う");
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(2);
        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(1);
        p.Assert(!finished, "アニメ終了前になぞり選択が完了");
        p.Assert(ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 1, "左解放後も選択1件");
        p.Note($"選択時刻:{Time.time-start:F3}s renderer数:{renderers.Length}");
        await p.WaitSeconds(0.5f);
        if (cancelEarly)
        {
            SemanticInput.KeyDown(Key.Escape);
            await UniTask.DelayFrame(3);
            p.Assert(!finished, "アニメ終了前にESC取消が完了");
            p.Assert(!HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount), "取消で選択件数が消える");
            p.Note($"取消観測:{Time.time-start:F3}s 終了済み:{finished}");
            SemanticInput.KeyUp(Key.Escape);
            await p.WaitSeconds(0.5f);
        }
        await AimAtGround(p, new Vector3Int(8,32,8));
        await p.Until(() => finished, 5f, "本番の設置アニメ完了通知を受信");
        await UniTask.DelayFrame(2);
        // ホバーを外しても選択の本体色が残ることを確認する
        // Verify that selection body color survives after moving hover away
        var previewShader = MaterialConst.GetPreviewPlaceBlockMaterial().shader;
        var redCount = 0;
        foreach (var renderer in renderers)
        foreach (var material in renderer.sharedMaterials)
        {
            if (material.shader != previewShader || !material.HasProperty(MaterialConst.PreviewColorPropertyName)) continue;
            var color = material.GetColor(MaterialConst.PreviewColorPropertyName);
            var expected = MaterialConst.NotPlaceableColor;
            if (Mathf.Abs(color.r-expected.r) < 0.01f && Mathf.Abs(color.g-expected.g) < 0.01f && Mathf.Abs(color.b-expected.b) < 0.01f) redCount++;
            p.Note($"実材質:{material.shader.name} color:{color}");
        }
        p.Note($"終了通知:{finishedAt-start:F3}s red材質数:{redCount} 取消:{cancelEarly}");
        p.Assert(cancelEarly ? redCount == 0 : redCount > 0, "実本体の赤表示が選択/取消状態と一致");
        p.Assert(p.GetBlock(cell) != null, "プレビュー検証中はサーバー対象を保持");
        await p.Screenshot(cancelEarly ? "02-cancelled-no-red" : "01-selection-red-after-animation");
        if (!cancelEarly)
        {
            // 完了後に適用された本体色もESCで解除できることを確認する
            // Verify Escape also clears body colors applied after animation completion
            await p.PressKey(Key.Escape);
            p.Assert(!HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount), "完了後ESCで選択件数が消える");
            var redAfterCancel = 0;
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (material.shader != previewShader || !material.HasProperty(MaterialConst.PreviewColorPropertyName)) continue;
                var color = material.GetColor(MaterialConst.PreviewColorPropertyName);
                var expected = MaterialConst.NotPlaceableColor;
                if (Mathf.Abs(color.r-expected.r) < 0.01f && Mathf.Abs(color.g-expected.g) < 0.01f && Mathf.Abs(color.b-expected.b) < 0.01f) redAfterCancel++;
            }
            p.Note($"完了後ESC: red材質数={redAfterCancel}");
            p.Assert(redAfterCancel == 0, "完了後ESCで同じ本体Renderer群の赤が消える");
            p.Assert(p.GetBlock(cell) != null, "完了後ESCでもサーバー対象を保持");
            await p.Screenshot("03-completed-then-cancelled-no-red");
        }
        await p.PressKey(Key.G);
        await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    }
});
