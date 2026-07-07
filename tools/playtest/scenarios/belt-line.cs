// S字ベルトコンベア搬送ライン検証: 方向転換2回（北→東→北）を含む9本のベルトで
// 鉄インゴット10個を搬送し、末端のコンベアチェストへ全数収納されることを録画付きで検証する
// S-shaped belt line verification: 9 belts with two corner turns (N->E->N) carry 10 iron ingots
// into a terminal conveyor chest, all verified on video
//
// 教訓: 「木のチェスト」はinputConnectsが空でベルト搬入不可。搬入先は「木のコンベアチェスト」
// (3x2x4、入力コネクタ=ローカル(1,0,0)セルへ南(0,0,-1)から接続)を使う
// Lesson: plain wooden chests have empty inputConnects and reject belt input; use the conveyor
// chest (3x2x4, input connector at local (1,0,0) fed from the south) as the terminal instead
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-line", options, async p =>
{
    await p.SetupFlatGround();

    // 北向き5本(2,32,2..6) → 東向き3本(2..4,32,7) → 北向き1本(5,32,7) のS字ライン
    // S-line: 5 north belts (2,32,2..6) -> 3 east belts (2..4,32,7) -> 1 north belt (5,32,7)
    for (var z = 2; z <= 6; z++) p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(2, 32, z), BlockDirection.North);
    for (var x = 2; x <= 4; x++) p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(x, 32, 7), BlockDirection.East);
    p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(5, 32, 7), BlockDirection.North);

    // コンベアチェスト原点(4,32,8): 入力セル(5,32,8)が(5,32,7)の北向きベルトから搬入を受ける
    // Conveyor chest origin (4,32,8): its input cell (5,32,8) receives from the north belt at (5,32,7)
    var chestPosition = new Vector3Int(4, 32, 8);
    p.PlaceBlockDirect("木のコンベアチェスト", chestPosition, BlockDirection.North);

    // ライン接続の健全性を設置直後に検証（今回の失敗クラスを早期検出）
    // Verify line connectivity right after placement (catches this failure class early)
    var tailBelt = p.GetBlock(new Vector3Int(5, 32, 7));
    var tailTargets = tailBelt.GetComponent<Game.Block.Component.BlockConnectorComponent<IBlockInventory, Game.Block.Interface.Component.ConnectJudge.DefaultConnectJudge>>().ConnectedTargets.Count;
    p.Assert(tailTargets == 1, "末端ベルトがチェストに接続している");

    // クライアント側の出現を待ってライン全景を撮影
    // Wait for client-side spawn, then capture the whole line
    await p.WaitBlockGameObject(chestPosition);
    await p.Screenshot("01-line-built");

    // 先頭ベルトへ鉄インゴットを0.5秒間隔で10個投入（映像で流れが見えるように間隔を空ける）
    // Feed 10 iron ingots into the head belt at 0.5s intervals (spaced so the flow is visible on video)
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
