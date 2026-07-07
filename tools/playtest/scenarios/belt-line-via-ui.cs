// S字ベルトライン等価性検証（UI経路版）: belt-line.csと同一のS字ライン＋同一assertを、
// サーバー直叩きではなくキーマウス操作経路（B→ビルドメニュー→プレビュー→クリック/ドラッグ設置）のみで構築する
// S-belt-line equivalence scenario (UI route): builds the same S-line with the same asserts as belt-line.cs,
// but only through the key/mouse route (B -> build menu -> preview -> click/drag placement), never the direct server path
//
// 設置の内訳: ドラッグ2回（北5本・東3本、向きは経路から自動解決）＋単クリック2回（北ベルト1本・コンベアチェスト、
// 向きはデフォルトNorth）。ブロックは未解放のためサーバー側アンロック→UnlockedEventPacket同期を先に行う
// Placement breakdown: two drags (5 north + 3 east, direction auto-resolved from the path) and two single clicks
// (one north belt + the conveyor chest, default North). Blocks start locked, so unlock server-side first
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-line-via-ui", options, async p =>
{
    await p.SetupFlatGround();

    // ライン中央付近へワープし、トップダウンカメラの視界に設置範囲全体を収める
    // Warp near the line center so the top-down camera covers the whole build area
    p.WarpPlayer(new Vector3(4f, 33.5f, 5f));

    // UI設置の前提を整える: アンロック＋建設コスト付与（クライアント在庫反映待ち込み）
    // Set up UI placement prerequisites: unlock plus construction cost (waits for client inventory sync)
    await p.PrepareBlockForUiPlacement("ベルトコンベア", 15);
    await p.PrepareBlockForUiPlacement("木のコンベアチェスト", 2);

    // UI経路のみでS字ラインを構築: 北5本→東3本→北1本→コンベアチェスト
    // Build the S-line via UI only: 5 north belts, 3 east belts, 1 north belt, then the conveyor chest
    await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), new Vector3Int(2, 32, 6));
    await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 7), new Vector3Int(4, 32, 7));
    await p.PlaceBlockViaUi("ベルトコンベア", new Vector3Int(5, 32, 7), BlockDirection.North);
    var chestPosition = new Vector3Int(4, 32, 8);
    await p.PlaceBlockViaUi("木のコンベアチェスト", chestPosition, BlockDirection.North);
    await p.ExitToGameScreen();

    // ライン接続の健全性を設置直後に検証（belt-line.csと同一assert）
    // Verify line connectivity right after placement (same assert as belt-line.cs)
    var tailBelt = p.GetBlock(new Vector3Int(5, 32, 7));
    var tailTargets = tailBelt.GetComponent<Game.Block.Component.BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count;
    p.Assert(tailTargets == 1, "末端ベルトがチェストに接続している");

    await p.WaitBlockGameObject(chestPosition);
    await p.Screenshot("01-line-built");

    // 先頭ベルトへ鉄インゴットを0.5秒間隔で10個投入（belt-line.csと同一）
    // Feed 10 iron ingots into the head belt at 0.5s intervals (same as belt-line.cs)
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    var headBelt = p.GetBlock(new Vector3Int(2, 32, 2)).GetComponent<VanillaBeltConveyorComponent>();
    for (var i = 0; i < 10; i++)
    {
        headBelt.InsertItem(ServerContext.ItemStackFactory.Create(itemId, 1), InsertItemContext.Empty);
        await p.WaitSeconds(0.5f);
    }
    await p.Screenshot("02-items-flowing");

    // チェスト内の鉄インゴット数を数えるヘルパー
    // Helper counting iron ingots inside the chest
    var chestComponent = p.GetBlock(chestPosition).GetComponent<VanillaChestComponent>();
    System.Func<int> countInChest = () =>
    {
        var total = 0;
        foreach (var stack in chestComponent.InventoryItems)
        {
            if (stack.Id == itemId) total += stack.Count;
        }
        return total;
    };

    // 到着を条件待機で検証（固定sleepなし）: 最初の1個→全10個
    // Verify arrival via condition waits (no fixed sleeps): first item, then all ten
    await p.Until(() => countInChest() >= 1, 90f, "最初のアイテムがチェストに到着");
    await p.Screenshot("03-first-arrived");
    await p.Until(() => countInChest() >= 10, 120f, "全10個がチェストに到着");
    p.Assert(countInChest() == 10, "チェスト内の鉄インゴットがちょうど10個（紛失・重複なし）");
    await p.Screenshot("04-all-arrived");
});
