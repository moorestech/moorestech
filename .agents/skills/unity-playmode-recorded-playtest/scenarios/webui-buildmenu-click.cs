// Web UIクリック実証: DOM矩形からCEF座標を解決し、実マウス経路でベルトコンベアを選択・設置する
// Web UI click proof: resolves CEF coordinates from a DOM rect and selects/places a belt through real mouse input
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("webui-buildmenu-click", options, async p =>
{
    // 無料設置と平坦足場を一括設定し、UI経路の前提をナレーションへ残す
    // Configure free placement and flat ground together, narrating the UI-route prerequisites
    p.Note("デバッグ環境を整え、Web UIビルドメニューからベルトコンベアを選択する");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // Bキーでメニューを開き、Reactパネルが実際にクリック可能になるまで待つ
    // Open the menu with B and wait until the React panel is genuinely clickable
    p.Note("Bキーでビルドメニューを開き、DOM要素の表示を確認する");
    await p.PressKey(Key.B);
    await p.UntilWebUiElement("build-menu-panel", 15f);

    // BlockId由来のtestidを使ってベルトをクリックし、設置モードへの遷移を検証する
    // Click the belt by its BlockId-derived testid and verify the transition into placement mode
    p.Note("DOM中心へマウスを移動してベルトコンベアをクリックする");
    await p.ClickBuildMenuBlock("ベルトコンベア");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "Web UIクリックでPlaceBlockへ遷移した");

    // 足場上の原点へ照準して1個設置し、サーバーワールドのBlockIdまで確認する
    // Aim at one scaffold origin, place once, and verify the BlockId in the server world
    var placeOrigin = new Vector3Int(0, 32, 2);
    p.Note($"ベルトコンベアを1個設置する: {placeOrigin}");
    await p.AimAtPlaceOrigin("ベルトコンベア", placeOrigin);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(placeOrigin) != null, 15f, $"ベルトコンベア設置反映: {placeOrigin}");

    var placedBlock = p.GetBlock(placeOrigin);
    var beltBlockId = PlaytestBlockOps.ResolveBlockId("ベルトコンベア");
    p.Assert(placedBlock != null && placedBlock.BlockId == beltBlockId, "指定位置にベルトコンベアが設置された");
    p.Note("Web UI選択から実設置までの確認が完了した");
    await p.Screenshot("01-webui-belt-placed");
});
