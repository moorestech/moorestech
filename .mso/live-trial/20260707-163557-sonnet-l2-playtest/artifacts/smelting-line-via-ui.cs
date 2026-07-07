// 石窯精錬ライン検証(UI経路): 搬入ベルト→石窯→搬出ベルト→木のコンベアチェストの4ブロックを
// すべてビルドメニューのキーマウ操作(クリック設置)で構築し、鉄鉱石の粉5個から鉄インゴット5個が
// 紛失・重複なくチェストへ届くことを検証する
// Smelting line via UI: build feed-belt -> stone furnace -> output-belt -> conveyor chest entirely
// through the build-menu key/mouse (click-placement) route, verifying 5 iron ore powder yield exactly
// 5 iron ingots delivered to the chest with no loss or duplication
//
// 石窯(ElectricMachine型・requiredPower=0の燃料式炉)は鉄鉱石の粉+燃料(木炭/原木)の2入力レシピしか
// 存在しないため、燃料はDirect投入(検証対象外の状態構築)とし、UI経路で検証するベルトは鉱石搬入のみ
// The stone furnace (ElectricMachine type, requiredPower=0, fuel-based) only has 2-input recipes
// (ore + charcoal/log), so fuel is direct-inserted as out-of-scope setup; the UI-route belt under
// test carries only the ore
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

    // ライン中央(石窯周辺)へワープし、トップダウンカメラの視界に設置範囲全体を収める
    // Warp near the line center (around the furnace) so the top-down camera covers the whole build area
    p.WarpPlayer(new Vector3(-14f, 33.5f, -13f));

    // UI設置の前提を整える: アンロック+建設コスト付与(クライアント在庫反映待ち込み)
    // Set up UI placement prerequisites: unlock plus construction cost (waits for client inventory sync)
    await p.PrepareBlockForUiPlacement("ベルトコンベア", 4);
    await p.PrepareBlockForUiPlacement("石窯", 1);
    await p.PrepareBlockForUiPlacement("木のコンベアチェスト", 1);

    // 電気系ブロック設置バグの回避策: ビルドメニューの建設コスト経路は原材料(砕いた石材/レンガ)のみを
    // 付与し「石窯」アイテム自体は所持させないため、ElectricWireAutoConnectPreview.TrySelectWireの
    // `virtualCounts.GetValueOrDefault(placingItemId) < 1` ガードでプレビューが常に設置不可(赤)になる。
    // サーバー側PlaceBlockProtocolは接続先ワイヤー候補が0件なら無条件で許可するため実際は設置できる
    // はずだが、クライアントプレビューが先にブロックしクリックが機能しない。「石窯」を1個所持させて
    // このガードだけ回避する(建設コストの消費対象には含まれないため実プレイの数量には影響しない)
    // Workaround for an electric-block placement bug: the build-menu construction-cost route grants
    // only raw materials (crushed stone/brick), never the "stone furnace" item itself, so
    // ElectricWireAutoConnectPreview.TrySelectWire's `virtualCounts.GetValueOrDefault(placingItemId) < 1`
    // guard keeps the placement preview permanently blocked (red). The server's PlaceBlockProtocol would
    // actually allow it since there are zero wire targets in range, but the client preview blocks first.
    // Granting one stone-furnace item works around just this guard (it is not part of the consumed
    // construction cost, so it does not affect real gameplay quantities)
    var stoveItemId = PlaytestItemOps.ResolveItemId("石窯");
    await p.GiveItem("石窯", 1);
    await p.Until(() => PlaytestItemOps.CountItemClientSide(stoveItemId) >= 1, 15f, "石窯アイテムのクライアント反映待ち(電気ブロック設置プレビュー回避)");

    // 搬入ベルト→石窯→搬出ベルト→チェストをUI経路のみ(クリック設置)で構築
    // Feed belt (-15,32,-16)->North が石窯の入力コネクタ(0,0,0)offsetへ、石窯の出力コネクタ(1,0,2)offset
    // が搬出ベルト(-14,32,-12)へ、そのままチェスト入力コネクタ(1,0,0)offsetの(-14,32,-11)へ直結する
    // Build feed belt -> furnace -> output belt -> chest via the UI route only (click placement)
    // Feed belt at (-15,32,-16) facing North feeds the furnace's (0,0,0)-offset input connector; the
    // furnace's (1,0,2)-offset output connector feeds the output belt at (-14,32,-12), which directly
    // feeds the chest's (1,0,0)-offset input connector at (-14,32,-11)
    var feedBeltPos = new Vector3Int(-15, 32, -16);
    var furnacePos = new Vector3Int(-15, 32, -15);
    var outputBeltPos = new Vector3Int(-14, 32, -12);
    var chestPos = new Vector3Int(-15, 32, -11);

    // 設置順は「低い/未設置のブロックの向こう側を先に狙う」形にならないよう、石窯(高さ2)を最後に置く。
    // 石窯を先に置いてからその北隣セルを狙うと、設置レイキャストが石窯の屋根コライダーに当たり
    // y=34(石窯の屋根の上)へ誤設置される実プロダクトバグを確認したため、この順序がその回避策も兼ねる
    // Place the stone furnace (height 2) last so its neighbors are never aimed at "through" it first.
    // Placing the furnace before its north neighbor caused the placement raycast to hit the furnace's
    // roof collider and misplace the belt on top of the roof (y=34) instead of the ground cell — a
    // real product bug this ordering also works around
    await p.PlaceBlockViaUi("ベルトコンベア", feedBeltPos, BlockDirection.North);
    await p.PlaceBlockViaUi("ベルトコンベア", outputBeltPos, BlockDirection.North);
    await p.PlaceBlockViaUi("木のコンベアチェスト", chestPos, BlockDirection.North);
    await p.PlaceBlockViaUi("石窯", furnacePos, BlockDirection.North);
    await p.ExitToGameScreen();

    // ライン全景を撮るため、石窯の footprint 外(東側)の見晴らしの良い位置へ再ワープする
    // (設置用のワープ地点は石窯 footprint 内で、そのままだと石窯の中に埋まった絵になるため)
    // Re-warp outside the furnace's footprint (to the east) for a clear overview shot — the
    // placement warp point sits inside the furnace's footprint, which would otherwise render
    // as the camera embedded inside the furnace mesh
    p.WarpPlayer(new Vector3(-11f, 33.5f, -13f));

    // ライン接続の健全性を設置直後に検証: 搬入ベルト→石窯、石窯→搬出ベルト、搬出ベルト→チェスト
    // Verify line connectivity right after placement: feed belt->furnace, furnace->output belt, output belt->chest
    var feedBeltConnections = p.GetBlock(feedBeltPos).GetComponent<Game.Block.Component.BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count;
    p.Assert(feedBeltConnections == 1, "搬入ベルトが石窯に接続している");
    var furnaceConnections = p.GetBlock(furnacePos).GetComponent<Game.Block.Component.BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count;
    p.Assert(furnaceConnections == 1, "石窯が搬出ベルトに接続している");
    var outputBeltConnections = p.GetBlock(outputBeltPos).GetComponent<Game.Block.Component.BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count;
    p.Assert(outputBeltConnections == 1, "搬出ベルトがチェストに接続している");

    await p.WaitBlockGameObject(chestPos);
    await p.Screenshot("01-line-built");

    // 燃料(木炭)を石窯へDirect投入: 精錬レシピは鉱石+燃料の2入力のため、検証対象外の前提として直接補充する
    // (鉄鉱石の粉1+木炭3→鉄インゴット1、5回分で木炭15個が必要)
    // Direct-insert charcoal fuel into the furnace: the recipe needs 2 inputs (ore+fuel), so fuel is
    // supplied as out-of-scope setup rather than through the tested belt route
    // (1 ore + 3 charcoal -> 1 ingot; 5 cycles need 15 charcoal)
    var charcoalId = PlaytestItemOps.ResolveItemId("木炭");
    var furnaceInventory = p.GetBlock(furnacePos).GetComponent<VanillaMachineBlockInventoryComponent>();
    furnaceInventory.InsertItem(ServerContext.ItemStackFactory.Create(charcoalId, 15), InsertItemContext.Empty);

    // 先頭ベルトへ鉄鉱石の粉を0.5秒間隔で5個投入
    // Feed 5 iron ore powder into the head belt at 0.5s intervals
    var oreId = PlaytestItemOps.ResolveItemId("鉄鉱石の粉");
    var ingotId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    var headBelt = p.GetBlock(feedBeltPos).GetComponent<VanillaBeltConveyorComponent>();
    for (var i = 0; i < 5; i++)
    {
        headBelt.InsertItem(ServerContext.ItemStackFactory.Create(oreId, 1), InsertItemContext.Empty);
        await p.WaitSeconds(0.5f);
    }
    await p.Screenshot("02-ore-flowing");

    // チェスト内の鉄インゴット数を数えるヘルパー
    // Helper counting iron ingots inside the chest
    var chestComponent = p.GetBlock(chestPos).GetComponent<VanillaChestComponent>();
    System.Func<int> countInChest = () =>
    {
        var total = 0;
        foreach (var stack in chestComponent.InventoryItems)
        {
            if (stack.Id == ingotId) total += stack.Count;
        }
        return total;
    };

    // 到着を条件待機で検証(固定sleepなし): 最初の1個→全5個。精錬は1回20秒×5回分の処理時間が必要
    // Verify arrival via condition waits (no fixed sleeps): first ingot, then all five. Smelting needs
    // 5 cycles at 20s each
    await p.Until(() => countInChest() >= 1, 180f, "最初の鉄インゴットがチェストに到着");
    await p.Screenshot("03-first-arrived");
    await p.Until(() => countInChest() >= 5, 240f, "全5個の鉄インゴットがチェストに到着");
    p.Assert(countInChest() == 5, "チェスト内の鉄インゴットがちょうど5個(紛失・重複なし)");
    await p.Screenshot("04-all-arrived");
});
