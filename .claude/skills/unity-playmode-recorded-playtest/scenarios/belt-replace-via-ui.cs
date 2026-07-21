// ベルトリプレース設置検証（UI経路）: 北向きベルト5本をドラッグ設置→搬送品3個を載せた状態で
// 同じセル列を逆方向へリプレースドラッグし、向きがSouthへ置き換わり搬送品が保持されることを検証する
// Belt replace-placement scenario (UI route): drag-place 5 north belts, load 3 transit items,
// then replace-drag the same cells in reverse and verify direction flips to South with items preserved
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-replace-via-ui", options, async p =>
{
    await p.SetupFlatGround();

    // 設置カメラは北向きのため、ライン南側に立ち全セルを前方視界に収める
    // The placement camera faces north, so stand south of the line to keep every cell in view
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -1f));

    // アンロック＋建設コスト付与（初回5本＋リプレース差額ゼロ精算の余裕込み）
    // Unlock and grant construction cost (5 belts plus margin for the net-zero replace settlement)
    await p.PrepareBlockForUiPlacement("ベルトコンベア", 10);

    p.Note("北向きベルト5本をドラッグ設置");
    await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), new Vector3Int(2, 32, 6));
    await p.ExitToGameScreen();

    // 初期設置の向きを検証する
    // Verify the initial placement directions
    var allNorth = true;
    for (var z = 2; z <= 6; z++)
    {
        if (p.GetBlock(new Vector3Int(2, 32, z)).BlockPositionInfo.BlockDirection != BlockDirection.North) allNorth = false;
    }
    p.Assert(allNorth, "初期設置: 5本すべて北向き");
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

    p.Note("同じセル列を逆方向へリプレースドラッグ");
    // 南側カメラから遠セルの接地面を狙うと手前ベルトがレイを遮るため、既存ベルトの天面(y=33平面)を照準する
    // Aiming far ground cells from the south camera gets occluded by near belts, so target the belt-top plane (y=33)
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    var replaceFromAim = PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(2, 33, 6), BlockDirection.North);
    var replaceToAim = PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(2, 33, 2), BlockDirection.North);
    await PlaytestUiOps.DragPlace(replaceFromAim, replaceToAim);
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
