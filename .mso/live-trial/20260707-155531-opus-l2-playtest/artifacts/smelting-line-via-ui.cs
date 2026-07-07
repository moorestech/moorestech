// 精錬ライン検証（UI経路版）: ブロック設置はすべてキーマウ操作経路（B→ビルドメニュー→プレビュー→クリック/ドラッグ）のみで構築する。
// 鉄鉱石の粉をベルトで石窯へ搬入→石窯が精錬→鉄インゴットを別ベルトで木のコンベアチェストへ搬入するラインを組み、粉5個投入→インゴット5個全数到達を検証する
// Smelting-line scenario (UI route): build everything only through the key/mouse route (B -> build menu -> preview -> click/drag).
// A belt carries iron-ore powder into the stone oven, which smelts it; a second belt carries the iron ingots into the conveyor chest. Feed 5 powder, verify all 5 ingots arrive.
//
// 石窯レシピは「鉄鉱石の粉1＋原木5→鉄インゴット1（20秒）」の2入力。原木は搬送検証の対象外なので石窯へ直接プリロードし、検証対象の粉のみベルト経路で流す。
// The oven recipe takes two inputs (1 powder + 5 logs -> 1 ingot, 20s). Logs are not the transport under test, so they are pre-loaded directly into the oven; only the powder flows through the belt route being verified.
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("smelting-line-via-ui", options, async p =>
{
    await p.SetupFlatGround();

    // 設置範囲(x -14〜-12, z -16〜-5)の中央付近へワープし、トップダウンカメラの視界に全域を収める
    // Warp near the center of the build area (x -14..-12, z -16..-5) so the top-down camera covers everything
    p.WarpPlayer(new Vector3(-13f, 33.5f, -11f));

    // UI設置の前提: アンロック＋建設コスト付与（クライアント在庫反映待ち込み）
    // UI placement prerequisites: unlock plus construction cost (waits for client inventory sync)
    await p.PrepareBlockForUiPlacement("ベルトコンベア", 6);
    await p.PrepareBlockForUiPlacement("石窯", 1);
    await p.PrepareBlockForUiPlacement("木のコンベアチェスト", 1);

    // 【発見した実バグの回避】電気ブロック(石窯)はビルドメニューの建設コスト経路だけでは設置プレビューが赤(不可)になる。
    // ElectricWireAutoConnectPreview.TrySelectWireがブロックアイテム自体の在庫を要求するため。ここでは石窯アイテムを与えて回避する（設置操作自体はUI経路のまま）
    // [Workaround for a real bug] Electric blocks (the oven) show a red/unplaceable preview via the build-menu construction-cost path alone,
    // because ElectricWireAutoConnectPreview.TrySelectWire demands the block item itself in inventory. Give the oven item to work around it (placement is still the UI route)
    var ovenItemId = PlaytestItemOps.ResolveItemId("石窯");
    await p.GiveItem("石窯", 1);
    await p.Until(() => PlaytestItemOps.CountItemClientSide(ovenItemId) >= 1, 15f, "石窯アイテムがクライアント在庫へ同期");

    // 石窯レシピ（鉄鉱石の粉＋原木5→鉄インゴット1）はマスタでinitialUnlocked=trueのため明示アンロック不要
    // The oven recipe (powder + 5 logs -> 1 ingot) is initialUnlocked=true in the master, so no explicit unlock is needed

    // UI経路のみでライン構築。座標: 搬入ベルト3本(北)→石窯→搬出ベルト2本(北)→コンベアチェスト
    // Build the line via UI only. Layout: 3 feed belts (north), oven, 2 output belts (north), conveyor chest
    var feedHead = new Vector3Int(-14, 32, -16);
    var ovenOrigin = new Vector3Int(-14, 32, -13);
    var chestOrigin = new Vector3Int(-14, 32, -8);

    // 先にベルト2系統を平地へ敷く。背の高い(2段)石窯を後に置くことで、ベルト設置レイキャストが石窯天面に当たり傾斜ベルト化する不具合を避ける
    // Lay both belt runs on flat ground first. Placing the tall (2-high) oven afterward avoids the belt-placement raycast hitting the oven top and yielding a slope belt
    await p.DragPlaceViaUi("ベルトコンベア", feedHead, new Vector3Int(-14, 32, -14));
    await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(-13, 32, -10), new Vector3Int(-13, 32, -9));

    // ベルトの間に石窯・その先にチェストを設置（近接ベルトは1段のみで石窯設置のほぼ垂直なレイキャストを遮らない）
    // Drop the oven between the belts and the chest beyond them (the 1-high belts don't block the oven's near-vertical placement ray)
    await p.PlaceBlockViaUi("石窯", ovenOrigin, BlockDirection.North);
    await p.PlaceBlockViaUi("木のコンベアチェスト", chestOrigin, BlockDirection.North);
    await p.ExitToGameScreen();

    // ライン接続の健全性を投入前に検証（受け側inputConnectsが繋がっているか）
    // Verify line connectivity before feeding (that the receiver inputConnects are actually linked)
    var feedTailBelt = p.GetBlock(new Vector3Int(-14, 32, -14));
    var feedTailTargets = feedTailBelt.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count;
    p.Assert(feedTailTargets == 1, "搬入末端ベルトが石窯に接続している");

    var oven = p.GetBlock(ovenOrigin);
    var ovenOutTargets = oven.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count;
    p.Assert(ovenOutTargets == 1, "石窯の出力が搬出ベルトに接続している");

    var outTailBelt = p.GetBlock(new Vector3Int(-13, 32, -9));
    var outTailTargets = outTailBelt.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count;
    p.Assert(outTailTargets == 1, "搬出末端ベルトがチェストに接続している");

    await p.WaitBlockGameObject(chestOrigin);
    await p.Screenshot("01-line-built");

    // 原木25個を石窯へ直接プリロード（精錬の第2材料。搬送検証の対象外なのでサーバー直投入）
    // Pre-load 25 logs directly into the oven (the smelting's second ingredient; not the transport under test, so inject server-side)
    var logId = PlaytestItemOps.ResolveItemId("原木");
    oven.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(ServerContext.ItemStackFactory.Create(logId, 25));
    await p.Screenshot("02-logs-loaded");

    // 検証対象の鉄鉱石の粉5個を先頭搬入ベルトへ0.5秒間隔で投入
    // Feed the 5 iron-ore powders under test into the head feed belt at 0.5s intervals
    var powderId = PlaytestItemOps.ResolveItemId("鉄鉱石の粉");
    var headBelt = p.GetBlock(feedHead).GetComponent<VanillaBeltConveyorComponent>();
    for (var i = 0; i < 5; i++)
    {
        headBelt.InsertItem(ServerContext.ItemStackFactory.Create(powderId, 1), InsertItemContext.Empty);
        await p.WaitSeconds(0.5f);
    }
    await p.Screenshot("03-powder-fed");

    // チェスト内の鉄インゴット数を数えるヘルパー
    // Helper counting iron ingots inside the chest
    var ingotId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    var chestComponent = p.GetBlock(chestOrigin).GetComponent<VanillaChestComponent>();
    System.Func<int> countInChest = () =>
    {
        var total = 0;
        foreach (var stack in chestComponent.InventoryItems)
        {
            if (stack.Id == ingotId) total += stack.Count;
        }
        return total;
    };

    // 到着を条件待機で検証（固定sleepなし）: 精錬20秒×5＋搬送を見込みタイムアウト余裕
    // Verify arrival via condition waits (no fixed sleeps): 20s x 5 smelting plus transport, generous timeout
    await p.Until(() => countInChest() >= 1, 90f, "最初の鉄インゴットがチェストに到着");
    await p.Screenshot("04-first-ingot");
    await p.Until(() => countInChest() >= 5, 180f, "鉄インゴット5個全部がチェストに到着");
    p.Assert(countInChest() == 5, "チェスト内の鉄インゴットがちょうど5個（紛失・重複なし）");
    await p.Screenshot("05-all-arrived");
});
