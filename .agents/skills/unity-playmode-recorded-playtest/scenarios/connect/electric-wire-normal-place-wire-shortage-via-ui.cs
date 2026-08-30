// 通常設置の電線不足ゲートE2E(受け入れ6): 電線を持たずに既存設備の近くへ電柱を通常設置すると「アイテム不足： 銅のワイヤー」が出て設置されない
// 検証項目: 電線不足時のラベル文言と設置拒否、対照として電線を補充すると同じ操作で設置できる
// Normal placement wire-shortage gate E2E (acceptance 6): placing a pole near existing equipment without wire shows アイテム不足： 銅のワイヤー and is refused.
// Verifies: the shortage label and the refused placement, plus a control showing the same click succeeds once wire is supplied.
using System;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.EnergySystem;
using Game.UnlockState;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-normal-place-wire-shortage-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(8f, 34f, -6f));
    await p.WaitSeconds(0.5f);

    p.Note("開幕スキットをSkipインテントで飛ばす");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    await p.Until(() =>
    {
        var s = skitStore.GetCurrent();
        return s != null && skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 30f, "開幕スキットのSkipインテントが受理される");
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    // 電柱を解放し、建設コストちょうど1本分だけ渡す（配線用の余剰電線を持たせない）
    // Unlock the pole and grant exactly one pole's construction cost, leaving no spare wire for wiring
    var wireToolGuid = Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2");
    p.UnlockBlock("電柱");
    await p.GiveConstructionCost("電柱", 1);
    p.ServerService<IGameUnlockStateDataController>().UnlockConnectTool(wireToolGuid);

    // 既存の電気設備として電柱を1本直設置する（新設位置から距離3＝接続範囲内）
    // Place one existing pole as the neighbouring equipment (3 blocks from the new position, in range)
    var existingPolePos = new Vector3Int(5, 32, 4);
    var targetPos = new Vector3Int(8, 32, 4);
    p.PlaceBlockDirect("電柱", existingPolePos, BlockDirection.North);
    await p.WaitBlockGameObject(existingPolePos);

    TextMeshPro AutoConnectLabel() => UnityEngine.Object
        .FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(t => t.gameObject.name == "AutoConnectWireCostLabel");
    string AutoConnectLabelText()
    {
        var label = AutoConnectLabel();
        return label != null && label.gameObject.activeInHierarchy ? label.text : string.Empty;
    }

    var wireCount = p.CountItem("銅のワイヤー");
    p.Note($"所持している銅のワイヤーは{wireCount}本（電柱1本の建設コストちょうど＝配線用の余剰なし）");

    // ビルドメニューから電柱を選び、既存電柱の近くへ照準する
    // Select the pole from the build menu and aim near the existing pole
    p.Note("ビルドメニューから電柱を選び、既存電柱の3ブロック隣へ照準する");
    await p.OpenBuildMenuAndSelectBlock("電柱");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("電柱", targetPos, BlockDirection.North));
    await UniTask.DelayFrame(5);

    // 電線不足の文言が自動接続プレビューのラベルに出る
    // The shortage text appears on the auto-connect preview label
    await p.Until(() => AutoConnectLabelText().Contains("アイテム不足： 銅のワイヤー"), 10f, "受け入れ6: 電線不足時に『アイテム不足： 銅のワイヤー』が表示される");
    p.Note($"自動接続プレビューのラベル表示='{AutoConnectLabelText()}'");
    await p.Screenshot("01-wire-shortage-label");

    // クリックしても設置されない
    // Clicking does not place the block
    p.Note("この状態でクリックしても設置されないことを確認する");
    await p.ClickPlace();
    await p.WaitSeconds(2f);
    p.Assert(p.GetBlock(targetPos) == null, "受け入れ6: 電線不足のためクリックしても電柱は設置されない");
    p.Assert(p.CountItem("銅のワイヤー") == wireCount, "受け入れ6: 拒否されたので素材も消費されていない");
    await p.Screenshot("02-placement-refused");

    // 対照: 電線(銅のワイヤー)を補充すると同じ操作で設置できる
    // Control: supplying wire (銅のワイヤー) makes the same click succeed
    p.Note("対照実験: 銅のワイヤーを補充して同じ位置へ設置し直す");
    await p.GiveItem("銅のワイヤー", 20);
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("電柱", targetPos, BlockDirection.North));
    await UniTask.DelayFrame(5);
    await p.Until(() => AutoConnectLabelText().StartsWith("電線 x"), 10f, "対照: 補充後はコスト表示（電線 xN）へ変わる");
    p.Note($"補充後のラベル表示='{AutoConnectLabelText()}'");
    await p.Screenshot("03-after-wire-supplied");

    await p.ClickPlace();
    await p.Until(() => p.GetBlock(targetPos) != null, 15f, "対照: 電線が足りていれば同じ操作で設置される");
    await p.WaitBlockGameObject(targetPos);
    await p.WaitSeconds(1f);
    var placedConnector = p.GetBlock(targetPos).ComponentManager.GetComponent<IElectricWireConnector>();
    var existingConnector = p.GetBlock(existingPolePos).ComponentManager.GetComponent<IElectricWireConnector>();
    p.Assert(placedConnector.ContainsWireConnection(existingConnector.BlockInstanceId), "対照: 通常設置の自動接続で既存電柱と結線される");
    p.Note("電線を補充したら同じ操作で設置＋自動接続まで通った（拒否理由が電線不足だったことの裏取り）");
    await p.Screenshot("04-placed-with-wire");
});
