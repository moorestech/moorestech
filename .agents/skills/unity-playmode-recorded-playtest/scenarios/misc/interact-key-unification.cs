// シナリオ: Fキーインタラクト統合(ADR 0046)の通し検証。小石のF単押し取得、石窯のtooltip文言とF単押しでのSubInventory遷移、
//           2m超で対象が選ばれずtooltipが消えることを、実プレイと同じ入力経路で確かめる
// Scenario: end-to-end check of the unified interact key (ADR 0046): tap F to pick up a pebble, the stone oven's
//           tooltip line and tap-F transition into SubInventory, and that nothing is selected beyond 2m
// フェーズ1は自然地形の小石を使うため足場を作らない。足場生成はフェーズ2の直前に行う
// Phase 1 uses a pebble on natural terrain, so the scaffold is created only right before phase 2
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var pebbleMapObjectGuid = new Guid("c74efe49-52f3-403b-9c9a-b39eb1c85fce"); // mapObject「小石」(miningType=PickUp)
var ovenBlockName = "石窯"; // 3x2x3のElectricMachine。開けるブロックの代表
var flatGroundObjectName = "PlaytestFlatGround"; // PlaytestSetupが生成する足場のGameObject名
var testFieldTopY = 200f; // 自然地形（メサは最高でもy=90付近）より十分高い、何も無い高度
var ovenModelCenterOffset = new Vector3(1.5f, 1f, 1.5f); // 原点(南西角)から3x2x3のモデル中心まで

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("interact-key-unification", options, async p =>
{
    // 開幕スキットは全UI入力を塞ぐため、再生されていれば飛ばしてからGameScreen到達を待つ
    // The opening skit blocks every UI input, so skip it when it plays and then wait for the game screen
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");

    #region フェーズ1: 小石をF単押しで拾う / Phase 1: tap F to pick up a pebble

    p.Note("フェーズ1: 自然地形の小石へ寄ってF単押しで拾う");
    var mapObjectDatastore = UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    var pebble = mapObjectDatastore.SearchNearestMapObject(new HashSet<Guid> { pebbleMapObjectGuid }, p.PlayerPosition);
    p.Assert(pebble != null, "最寄りの未破壊の小石がクライアントで見つかった");

    var pebblePosition = pebble.transform.position;
    p.WarpPlayer(pebblePosition + new Vector3(0f, 1.5f, -1f));
    await p.WaitSeconds(1f);

    // cullableなmapObjectは遠距離で無効化されるため、寄ったあと有効化されるのを待つ
    // Cullable map objects are disabled at range, so wait for it to come back after warping in
    await p.Until(() => pebble.gameObject.activeInHierarchy, 10f, "小石のGameObjectが有効");
    var pebbleDistance = Vector3.Distance(p.PlayerPosition, pebblePosition);
    p.Note($"小石={pebblePosition} プレイヤー={p.PlayerPosition} 距離={pebbleDistance:F2}m");
    p.Assert(pebbleDistance <= 2f, "小石がインタラクト距離2m以内にある");

    await AimAtIfOnScreen(pebblePosition);
    await p.WaitSeconds(0.5f);
    await p.Screenshot("01-pebble-tooltip");
    p.Assert(FirstTooltipKey() == LocalizationKeys.Ui.Tooltip.NamedMineClick.Key, "小石のtooltipがFで採掘(namedMineClick)");

    var pebbleCountBeforePickUp = p.CountItem("小石");
    await p.PressInteract();
    await p.Until(() => pebbleCountBeforePickUp < p.CountItem("小石"), 15f, "F単押しで小石が増える");
    p.Note($"小石 {pebbleCountBeforePickUp} → {p.CountItem("小石")}");
    await p.Screenshot("02-pebble-picked-up");

    #endregion

    #region フェーズ2: 石窯をF単押しで開く / Phase 2: tap F to open the stone oven

    p.Note("フェーズ2: 上空の足場に石窯を設置してF単押しで開く");

    // 原点の足場は自然地形のメサに埋もれており、岩が石窯を隠し選定も奪うため、足場ごと誰もいない上空へ移す
    // The scaffold at the origin is buried in natural mesa terrain that hides the oven and steals the selection, so move it to empty sky
    await p.SetupFlatGround();
    GameObject.Find(flatGroundObjectName).transform.position = new Vector3(0f, testFieldTopY - 2f, 0f);
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    p.Assert(Mathf.Abs(p.PlayerPosition.y - testFieldTopY) < 0.5f, "移設した足場の上に着地した");

    // 石窯の原点は正面やや左に置く。原点は3x3の南西角で、選定距離もモデルも原点基準でずれるため実測して寄る
    // The oven origin goes slightly left of center: it is the 3x3 south-west corner that both the model and the selection distance hang off
    var ovenOrigin = new Vector3Int(-1, Mathf.RoundToInt(testFieldTopY), 1);
    p.PlaceBlockDirect(ovenBlockName, ovenOrigin, BlockDirection.North);
    var ovenGameObject = await p.WaitBlockGameObject(ovenOrigin);
    var ovenPosition = ovenGameObject.transform.position;
    p.Note($"石窯の原点={ovenPosition} プレイヤー={p.PlayerPosition} カメラ前方={Camera.main.transform.forward}");
    p.Assert(0.7f < Camera.main.transform.forward.z, "カメラが北(+z)を向いており石窯が画面内にある");
    await p.WaitSeconds(1f);

    var ovenDistance = Vector3.Distance(p.PlayerPosition, ovenPosition);
    p.Note($"石窯の原点までの距離={ovenDistance:F2}m");
    p.Assert(ovenDistance <= 2f, "石窯がインタラクト距離2m以内にある");

    await AimAtIfOnScreen(ovenPosition + ovenModelCenterOffset);
    await p.WaitSeconds(0.5f);
    await p.Screenshot("03-oven-outline-and-tooltip");
    p.Assert(FirstTooltipKey() == LocalizationKeys.Ui.Tooltip.InteractOpenBlock.Key, "石窯のtooltip先頭行がinteractOpenBlock");

    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 5f);
    p.Assert(p.CurrentUiState == UIStateEnum.SubInventory, "F単押しでSubInventoryへ遷移した");
    await p.Screenshot("04-oven-sub-inventory");

    #endregion

    #region フェーズ3: 2mを超えると選ばれない / Phase 3: nothing is selected beyond 2m

    p.Note("フェーズ3: ゲーム画面へ戻り石窯から3m離れてtooltipが消えることを確認する");
    // SubInventoryはBでは閉じない（キーヒントどおりTabのOpenInventoryで閉じる）
    // The sub inventory does not close on B; it closes with Tab (OpenInventory) as its key hint says
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.WarpPlayer(new Vector3(ovenPosition.x, p.PlayerPosition.y, ovenPosition.z - 3.5f));
    await p.WaitSeconds(1f);

    var farDistance = Vector3.Distance(p.PlayerPosition, ovenPosition);
    p.Note($"プレイヤー={p.PlayerPosition} 石窯までの距離={farDistance:F2}m");
    p.Assert(2f < farDistance, "石窯がインタラクト距離2mより遠い");

    await AimAtIfOnScreen(ovenPosition + ovenModelCenterOffset);
    await p.WaitSeconds(0.5f);
    p.Assert(!Tooltip().GetPresentation().Visible, "2m超ではtooltipが表示されない");
    await p.Screenshot("05-out-of-range-no-tooltip");

    #endregion

    p.Note("検証完了");

    #region Internal

    // tooltipの先頭行のローカライズキー。非表示なら空文字（Assertの差分が読めるようにする）
    // Localization key of the tooltip's first line; empty when hidden so a failed assert stays readable
    string FirstTooltipKey()
    {
        var presentation = Tooltip().GetPresentation();
        return presentation.Visible ? presentation.Lines[0].Key.Key : string.Empty;
    }

    // 表示状態の正本はDI登録されたIMouseCursorTooltip（uGUIのMouseCursorTooltipは書き込まれない残骸）
    // The authoritative presentation lives on the DI-registered IMouseCursorTooltip; the uGUI MouseCursorTooltip is an unwritten leftover
    IMouseCursorTooltip Tooltip()
    {
        return ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
    }

    // AimAtは画面外を渡すと例外で中断するため、投影を確かめてから照準する
    // AimAt aborts with an exception for off-screen points, so verify the projection before aiming
    async UniTask AimAtIfOnScreen(Vector3 worldPosition)
    {
        var screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        var isOnScreen = 0f < screenPosition.z && 0f <= screenPosition.x && screenPosition.x <= Screen.width && 0f <= screenPosition.y && screenPosition.y <= Screen.height;
        if (!isOnScreen)
        {
            p.Note($"照準対象{worldPosition}が画面外({screenPosition})のため近傍選定に委ねる");
            return;
        }

        await p.AimAt(worldPosition);
    }

    #endregion
});
