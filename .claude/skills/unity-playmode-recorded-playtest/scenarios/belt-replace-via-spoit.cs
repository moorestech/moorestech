// ベルトリプレース設置検証（スポイト経由UI）: 北向きベルト5本をサーバー直設置し搬送品3個を載せ、
// 既存ベルトをスポイト（ミドルクリック）→逆方向へリプレースドラッグして向き反転と搬送品保持を検証する
// Belt replace-placement scenario (spoit UI route): direct-place 5 north belts with 3 transit items,
// eyedrop an existing belt (middle click), replace-drag in reverse, verify flipped direction and preserved items
// ビルドメニュー（CEF Web UI）を使わないため、CEFのWS接続が無い環境でも実UIドラッグ経路を検証できる
// Avoids the build menu (CEF Web UI), so the real UI drag route is testable without a CEF WS connection
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-replace-via-spoit", options, async p =>
{
    await p.SetupFlatGround();

    // 設置カメラは北向きのため、ライン南側に立ち全セルを前方視界に収める
    // The placement camera faces north, so stand south of the line to keep every cell in view
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -1f));

    // リプレースのunlock検証を通すため事前にアンロック＋コスト付与（同型は差額ゼロ精算）
    // Unlock and grant cost up front so the replace unlock check passes (same-type nets to zero)
    await p.PrepareBlockForUiPlacement("ベルトコンベア", 10);

    p.Note("北向きベルト5本をサーバー直設置");
    for (var z = 2; z <= 6; z++)
    {
        p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(2, 32, z), BlockDirection.North);
    }
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 6));
    await p.Screenshot("01-north-line");

    p.Note("搬送品3個を先頭ベルトへ投入");
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    var headBelt = p.GetBlock(new Vector3Int(2, 32, 2)).GetComponent<VanillaBeltConveyorComponent>();
    for (var i = 0; i < 3; i++)
    {
        headBelt.InsertItem(ServerContext.ItemStackFactory.Create(itemId, 1), InsertItemContext.Empty);
        await p.WaitSeconds(0.5f);
    }

    // ライン上の搬送品総数を数えるヘルパー
    // Helper counting transit items across the line
    System.Func<int> countOnLine = () =>
    {
        var total = 0;
        for (var z = 2; z <= 6; z++)
        {
            var belt = p.GetBlock(new Vector3Int(2, 32, z));
            if (belt == null) continue;
            foreach (var item in belt.GetComponent<VanillaBeltConveyorComponent>().BeltConveyorItems)
            {
                if (item != null) total++;
            }
        }
        return total;
    };
    await p.Until(() => countOnLine() == 3, 30f, "搬送品3個がライン上にある");
    await p.Screenshot("02-items-on-line");

    p.Note("既存ベルトをスポイトして設置モードへ");
    await p.AimAt(new Vector3(2.5f, 32.2f, 4.5f));
    SemanticInput.MouseButtonDown(2);
    await UniTask.DelayFrame(2);
    SemanticInput.MouseButtonUp(2);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    await p.WaitSeconds(0.8f);

    p.Note("同じセル列を逆方向へリプレースドラッグ");
    // 南側カメラから遠セルの接地面を狙うと手前ベルトがレイを遮るため、既存ベルトの天面(y=33平面)を照準する
    // Aiming far ground cells from the south camera gets occluded by near belts, so target the belt-top plane (y=33)
    var fromAim = PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(2, 33, 6), BlockDirection.North);
    var toAim = PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(2, 33, 2), BlockDirection.North);
    await PlaytestUiOps.DragPlace(fromAim, toAim);
    await p.ExitToGameScreen();

    // リプレース結果: 5本すべて南向きへ置き換わり、搬送品が保持されている
    // Replace result: all 5 belts now face south and the transit items survive
    await p.Until(() =>
    {
        for (var z = 2; z <= 6; z++)
        {
            var belt = p.GetBlock(new Vector3Int(2, 32, z));
            if (belt == null || belt.BlockPositionInfo.BlockDirection != BlockDirection.South) return false;
        }
        return true;
    }, 15f, "リプレース: 5本すべて南向きへ置き換え");
    p.Assert(countOnLine() == 3, "リプレース後も搬送品3個が保持されている");
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    await p.Screenshot("03-replaced-south");
});
