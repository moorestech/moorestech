// 電線接続の範囲ボックス相互判定E2E検証(設置時自動接続経路): 電柱をUI設置し、範囲内の電柱へは自動接続され範囲外へは接続されないことを実証する
// 検証項目: 電柱設置の自動接続が「相互範囲内の最寄り電柱1本」を選ぶ。A-B距離6(範囲±6内)は自動接続成立、B-C距離10(範囲外)は接続されずCは独立
// Electric wire mutual range-box judgement E2E (auto-connect on placement): placing a pole auto-connects to an in-range pole, not to an out-of-range one.
// Verifies: pole placement auto-connect picks the nearest mutually-in-range pole. A-B distance 6 (within ±6) connects; B-C distance 10 (out of range) does not, leaving C isolated.
using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.EnergySystem;
using Game.UnlockState;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-mutual-range-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    // カメラは北(+Z)を向くためプレイヤーを南に置き、電柱は前方(高いZ)へ設置する
    // The camera faces north (+Z), so place the player to the south and the poles ahead (higher Z)
    p.WarpPlayer(new Vector3(8f, 34f, -3f));
    await p.WaitSeconds(0.5f);

    // 開幕スキット(Story)を表示中はビルドメニューが開けないためSkipインテントで飛ばしGameScreenへ抜ける
    // The opening skit (Story) blocks the build menu, so skip it via the intent path and reach GameScreen
    p.Note("開幕スキットをSkipインテントで飛ばす");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    await p.Until(() =>
    {
        var s = skitStore.GetCurrent();
        return s != null && skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 30f, "開幕スキットのSkipインテントが受理される");
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    // 電柱のUI設置前提を整える: アンロック＋建設コスト
    // Prepare UI placement of poles: unlock + construction cost
    await p.PrepareBlockForUiPlacement("電柱", 10);

    // 自動接続には電線connectToolの解放と消費アイテム(銅のワイヤー)が必要（未解放だと配線されず設置のみ）
    // Auto-connect needs the electricWire connectTool unlocked and its material item (銅のワイヤー); otherwise placement occurs without wiring
    p.ServerService<IGameUnlockStateDataController>().UnlockConnectTool(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"));
    await p.GiveItem("銅のワイヤー", 64);

    // 電柱3本の設置座標。A-B間はX距離6(電柱poleConnectionRange=13→±6の範囲内)、B-C間はZ距離9(範囲外)
    // Placement positions. A-B X-distance 6 (within 電柱 poleConnectionRange 13 -> ±6), B-C Z-distance 9 (out of range)
    var poleAPos = new Vector3Int(5, 32, 3);
    var poleBPos = new Vector3Int(11, 32, 3);
    var poleCPos = new Vector3Int(11, 32, 12);

    // 2つの電柱コネクタが相互に結線を保持していれば結線済みと判定する
    // Two poles are connected when either connector holds the other in its wire connections
    bool Connected(Vector3Int a, Vector3Int b)
    {
        var ca = p.GetBlock(a).ComponentManager.GetComponent<IElectricWireConnector>();
        var cb = p.GetBlock(b).ComponentManager.GetComponent<IElectricWireConnector>();
        return ca.ContainsWireConnection(cb.BlockInstanceId) || cb.ContainsWireConnection(ca.BlockInstanceId);
    }

    // ①電柱Aを設置。周囲に電柱が無いので自動接続は起きない
    // Place pole A. No neighboring poles yet, so nothing auto-connects
    p.Note("電柱Aを(2,2)へUI設置。周囲に電柱が無いため自動接続なし");
    await p.PlaceBlockViaUi("電柱", poleAPos, BlockDirection.North);
    await p.WaitSeconds(0.5f);
    await p.Screenshot("01-pole-a");

    // ②電柱Bを距離6(範囲内)へ設置。設置時自動接続が相互範囲内の最寄り電柱Aを選び結線する
    // Place pole B at distance 6 (in range). Placement auto-connect picks the nearest mutually-in-range pole A and connects
    p.Note("電柱Bを(11,3)へUI設置。A-BのX距離6は範囲±6内なので設置時自動接続でAと結線されるはず");
    await p.PlaceBlockViaUi("電柱", poleBPos, BlockDirection.North);
    await p.WaitSeconds(0.5f);
    await p.Until(() => Connected(poleAPos, poleBPos), 15f, "範囲内(距離6)のBは設置時自動接続でAと同一セグメントになる");
    p.Note("範囲内のA-Bが自動接続で同一セグメントに統合された");
    await p.Screenshot("02-pole-b-connected");

    // ③電柱Cを距離10(範囲外)へ設置。相互範囲内に電柱が無いため自動接続は起きずCは独立のまま
    // Place pole C at distance 10 (out of range). No pole is mutually in range, so no auto-connect and C stays isolated
    p.Note("電柱Cを(11,12)へUI設置。B-CのZ距離9は範囲±6外なので自動接続されないはず");
    await p.PlaceBlockViaUi("電柱", poleCPos, BlockDirection.North);
    await p.WaitSeconds(0.5f);
    // 十分待ってもCがA/Bと同一セグメントにならないことを確認する
    // Wait long enough and confirm C never joins A/B's segment
    await p.WaitSeconds(2f);
    p.Assert(!Connected(poleBPos, poleCPos), "範囲外(Z距離9)のB-Cは自動接続されず別セグメントのまま");
    p.Assert(!Connected(poleAPos, poleCPos), "範囲外のCはA-Bセグメントに含まれない");
    p.Note("範囲外のCは自動接続されず独立セグメントのまま。相互範囲判定がX距離6を許可しZ距離9を拒否した");
    await p.Screenshot("03-pole-c-rejected");

    // 最終状態: A-Bは同一セグメント、Cは別セグメント
    // Final state: A-B in one segment, C in another
    p.Assert(Connected(poleAPos, poleBPos), "最終確認: A-Bは同一セグメント");
    p.Assert(!Connected(poleAPos, poleCPos), "最終確認: Cは独立セグメント");
    await p.ExitToGameScreen();
});
