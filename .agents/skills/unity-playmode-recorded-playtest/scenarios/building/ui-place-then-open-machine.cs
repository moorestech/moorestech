// ビルドメニューのUI経路で機械を設置し、そのままF単押しで機械UI（Web側block inventory）まで開く通し検証
// End-to-end check: place a machine through the build-menu UI route, then tap F to open its machine UI (the web block inventory)
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

var ovenBlockName = "石窯"; // 3x2x3のElectricMachine。block inventoryを持つ代表ブロック
var flatGroundObjectName = "PlaytestFlatGround"; // PlaytestSetupが生成する足場のGameObject名
var testFieldTopY = 200f; // 自然地形（メサは最高でもy=90付近）より十分高い、何も無い高度
var ovenModelCenterOffset = new Vector3(1.5f, 1f, 1.5f); // 原点(南西角)から3x2x3のモデル中心まで

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("ui-place-then-open-machine", options, async p =>
{
    // 開幕スキットは全UI入力を塞ぐため最初に飛ばす
    // The opening skit blocks every UI input, so skip it first
    await p.SkipOpeningSkit();

    // 原点の足場は自然地形のメサに埋もれ、メサが照準レイと近傍選定を奪うため、足場ごと誰もいない上空へ移す
    // The scaffold at the origin is buried in mesa terrain that steals both the aim ray and the nearby selection, so move it into empty sky
    await p.SetupFlatGround();
    GameObject.Find(flatGroundObjectName).transform.position = new Vector3(0f, testFieldTopY - 2f, 0f);
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    p.Assert(Mathf.Abs(p.PlayerPosition.y - testFieldTopY) < 0.5f, "移設した足場の上に着地した");
    await p.PrepareBlockForUiPlacement(ovenBlockName, 1);

    var ovenOrigin = new Vector3Int(-1, Mathf.RoundToInt(testFieldTopY), 1);

    // ビルドメニュー→カテゴリ切替→エントリクリック→照準→クリック設置までをUI経路だけで通す
    // Drive build menu, category switch, entry click, aim, and click-place entirely through the UI route
    await p.PlaceBlockViaUi(ovenBlockName, ovenOrigin, BlockDirection.North);
    await p.ExitToGameScreen();
    var oven = await p.WaitBlockGameObject(ovenOrigin);
    p.Assert(p.GetBlock(ovenOrigin) != null, "UI経路で石窯がサーバーワールドへ設置された");
    await p.Screenshot("01-oven-placed-via-ui");

    // インタラクト距離2m以内へ寄り、モデル中心へ照準してからF単押しで開く
    // Move within the 2m interact range and aim at the model center before tapping F
    var ovenPosition = oven.transform.position;
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    p.Assert(Vector3.Distance(p.PlayerPosition, ovenPosition) <= 2f, "石窯がインタラクト距離2m以内にある");
    await p.AimAt(ovenPosition + ovenModelCenterOffset);
    await p.WaitSeconds(0.5f);

    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    p.Assert(p.CurrentUiState == UIStateEnum.SubInventory, "F単押しで機械UI（SubInventory）へ遷移した");

    // Unity側の状態だけでなく、Web側のblock inventoryパネルが実際に描画されていることまで確かめる
    // Verify not only the Unity-side state but that the web block inventory panel is actually rendered
    var blockInventoryDom = await PlaytestDomQuery.Query("block-inventory", 10f);
    p.Note($"[Web UI DOM] block-inventory found={blockInventoryDom.Found}");
    p.Assert(blockInventoryDom.Found, "Web側のblock inventoryパネルが描画されている");
    await p.Screenshot("02-machine-ui-opened");
});
