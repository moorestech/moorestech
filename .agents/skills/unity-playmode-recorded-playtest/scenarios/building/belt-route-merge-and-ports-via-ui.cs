// ベルト経路の出力口まわりの通し検証（ADR 0103）。経路が既存の出力口の前を避けずに通って合流すること、
// 始点で出力口の面を狙うとブロックから離れる向きになり、終点は入力口の面を狙ったときだけ差し込む向きになり他の面では R の向きのままになること、
// 終点が下り坂になること、分岐器と坂が通常ブロックと同じ単発設置になることを、実プレイヤーと同じキーマウス経路で確かめる
// End-to-end check around outputs for belt routes (ADR 0103): a route passes in front of an existing output and merges,
// a start aimed at an output face heads away from the block while an end heads in only on an input face and keeps the R heading on other faces,
// the end becomes a down slope, and splitters and slopes are placed singly like common blocks, all through the real key/mouse path
using System.Linq;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-route-merge-and-ports-via-ui", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(5f, 33.5f, -3f) });
    await p.SkipOpeningSkit();
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    // 足場は地形の山の中にあり、そのままでは設置物が録画に写らない
    // The scaffold sits inside a terrain mound, so placed blocks would not show on the recording
    p.HideMapObjects();
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");

    // ---------------------------------------------------------------- 1. 出力口の前を通って合流
    p.Note("フェーズ1: 既存ベルトの末端が (5,32,2) を向いている。そこを避けずに (2,32,2)→(8,32,2) を一直線に置く");
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(5, 32, 0), BlockDirection.North);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(5, 32, 1), BlockDirection.North);
    await p.WaitSeconds(0.5f);
    var beltsBeforeMerge = CountBelts();
    await p.RoutePlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), BlockDirection.East, new Vector3Int(8, 32, 2), BlockDirection.East);
    p.Assert(p.GetBlock(new Vector3Int(5, 32, 2)) != null && ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(5, 32, 2)) == BlockDirection.East, "出力口の前 (5,32,2) に東向きのベルトが置かれる");
    p.Assert(CountBelts() - beltsBeforeMerge == 7, $"迂回せず7セルで置かれる（実際 {CountBelts() - beltsBeforeMerge}）");
    await p.WaitBlockGameObject(new Vector3Int(8, 32, 2));
    await p.ExitToGameScreen();

    p.Note("フェーズ1: 既存ベルト側と経路の始点側の両方から荷物を入れ、合流して終点の先のチェストへ2個届く");
    var sideSource = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(5, 32, -1), BlockDirection.North);
    var routeSource = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(1, 32, 2), BlockDirection.East);
    var mergeSink = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(9, 32, 2), BlockDirection.East);
    await p.RunOnServer("両方の供給チェストへ鉄インゴット1個ずつ", () =>
    {
        sideSource.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 1));
        routeSource.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 1));
    });
    await p.Until(() => 2 <= CountOf(mergeSink, itemId), 60f, "合流して2個とも終点の先のチェストへ届く");
    await p.Screenshot("01-merge");

    // ---------------------------------------------------------------- 2. 始点で出力口の面を狙う
    p.Note("フェーズ2: 東向きミニチェストの出力口の面を始点に狙う。R を押さなくても始点はチェストから離れる東向きになる");
    var portChest = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(2, 32, 6), BlockDirection.East);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 6));
    p.WarpPlayer(new Vector3(6f, 33.5f, 4.5f));
    await p.WaitSeconds(0.5f);
    var beltsBeforePort = CountBelts();
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(new Vector3(3f, 32.5f, 6.5f));
    await p.ClickPlace();
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(7, 32, 6), BlockDirection.North));
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.North, BlockDirection.East);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(7, 32, 6)) != null, 15f, "出力口からの経路が置かれる");
    p.Assert(ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(3, 32, 6)) == BlockDirection.East, "出力口の前の始点は東向き（チェストから離れる向き）");
    p.Assert(CountBelts() - beltsBeforePort == 5, $"始点が東向きなので一直線の5セル（実際 {CountBelts() - beltsBeforePort}）");
    await p.WaitBlockGameObject(new Vector3Int(7, 32, 6));
    await p.ExitToGameScreen();
    var portSink = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(8, 32, 6), BlockDirection.East);
    await p.RunOnServer("出力口のチェストへ鉄インゴット1個", () => portChest.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 1)));
    await p.Until(() => 1 <= CountOf(portSink, itemId), 60f, "出力口から出た荷物が経路の先のチェストへ届く");
    await p.Screenshot("02-start-on-output-port");

    // ---------------------------------------------------------------- 2b. 終点で出力口の面を狙う
    p.Note("フェーズ2b: 別の東向きミニチェストの面を終点に狙う。終点はチェストへ差し込む西向きにならず、R で合わせた南向きのまま");
    await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(2, 32, 10), BlockDirection.East);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 10));
    p.WarpPlayer(new Vector3(7f, 33.5f, 8.5f));
    await p.WaitSeconds(0.5f);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(5, 32, 10), BlockDirection.North));
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.North, BlockDirection.West);
    await p.ClickPlace();
    await p.AimAt(new Vector3(3f, 32.5f, 10.5f));
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.West, BlockDirection.South);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(3, 32, 10)) != null, 15f, "出力口の前を終点にした経路が置かれる");
    p.Assert(ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(3, 32, 10)) == BlockDirection.South, "出力口の面を狙った終点は R の向き（南）");
    await p.WaitBlockGameObject(new Vector3Int(3, 32, 10));
    await p.ExitToGameScreen();

    p.Note("フェーズ2c: 口の無い木のチェストの面を終点に狙う。ここでも終点はチェストへ差し込む西向きにならず、R で合わせた北向きのまま");
    await p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 14), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 14));
    p.WarpPlayer(new Vector3(7f, 33.5f, 12.5f));
    await p.WaitSeconds(0.5f);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(5, 32, 14), BlockDirection.North));
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.North, BlockDirection.West);
    await p.ClickPlace();
    await p.AimAt(new Vector3(3f, 32.5f, 14.5f));
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.West, BlockDirection.North);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(3, 32, 14)) != null, 15f, "口の無い面の前を終点にした経路が置かれる");
    p.Assert(ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(3, 32, 14)) == BlockDirection.North, "口の無い面を狙った終点も R の向き（北）");
    await p.WaitBlockGameObject(new Vector3Int(3, 32, 14));
    await p.ExitToGameScreen();
    await p.Screenshot("03-end-on-block-face");

    p.Note("フェーズ2d: 東向きミニチェスト (2,32,10) の入力口の面（西の面）を終点に狙う。R は北のままでも、終点はチェストへ差し込む東向きになる");
    p.WarpPlayer(new Vector3(-2f, 33.5f, 8.5f));
    await p.WaitSeconds(0.5f);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(-1, 32, 10), BlockDirection.North));
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.North, BlockDirection.East);
    await p.ClickPlace();
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.East, BlockDirection.North);
    await p.AimAt(new Vector3(2f, 32.5f, 10.5f));
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(1, 32, 10)) != null, 15f, "入力口の前を終点にした経路が置かれる");
    p.Assert(ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(1, 32, 10)) == BlockDirection.East, "入力口の面を狙った終点は差し込む向き（東）");
    await p.WaitBlockGameObject(new Vector3Int(1, 32, 10));
    await p.ExitToGameScreen();
    await p.Screenshot("03b-end-on-input-port");

    // ---------------------------------------------------------------- 3. 終点が下り坂
    p.Note("フェーズ3: チェストの上 (10,33,6) を始点、隣の地面 (11,32,6) を終点にすると、終点が下り坂になる");
    await p.PlaceBlockDirect("木のチェスト", new Vector3Int(10, 32, 6), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(10, 32, 6));
    p.WarpPlayer(new Vector3(12.5f, 33.5f, 3f));
    await p.WaitSeconds(0.5f);
    var beltsBeforeDown = CountBelts();
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(new Vector3(10.5f, 33f, 6.5f));
    await PlaytestBeltRouteOps.RotateTo(p, BlockDirection.North, BlockDirection.East);
    await p.ClickPlace();
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(11, 32, 6), BlockDirection.North));
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(11, 32, 6)) != null, 15f, "下り坂の終点が置かれる");
    p.Assert(p.GetBlock(new Vector3Int(11, 32, 6)).BlockMasterElement.Name == "下りベルトコンベア", $"終点は下りベルトコンベア（実際 {p.GetBlock(new Vector3Int(11, 32, 6)).BlockMasterElement.Name}）");
    p.Assert(ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(11, 32, 6)) == BlockDirection.East, "下り坂の終点は東向き");
    p.Assert(CountBelts() - beltsBeforeDown == 2, $"始点と下り坂の終点の2セルだけ（実際 {CountBelts() - beltsBeforeDown}）");
    await p.WaitBlockGameObject(new Vector3Int(10, 33, 6));
    await p.WaitBlockGameObject(new Vector3Int(11, 32, 6));
    await p.ExitToGameScreen();
    // 設置位置の手前に立ったままだとアバターが坂を隠すので、東へずれてから撮る
    // Standing right in front hides the slope behind the avatar, so step east before the shot
    p.WarpPlayer(new Vector3(15.5f, 33.5f, 3f));
    await p.WaitSeconds(0.5f);
    await p.Screenshot("04-down-slope-end");

    // ---------------------------------------------------------------- 4. 分岐器・坂は単発設置
    p.Note("フェーズ4: 分岐器と上り坂を選んで1クリックすると、始点待ちにならずそのブロックが1個だけ置かれる");
    p.WarpPlayer(new Vector3(5f, 33.5f, -8f));
    await p.WaitSeconds(0.5f);
    var beltsBeforeSingle = CountBelts();
    await p.PlaceBlockViaUi("ベルトコンベア分岐器", new Vector3Int(3, 32, -4), BlockDirection.North);
    p.Assert(p.GetBlock(new Vector3Int(3, 32, -4)).BlockMasterElement.Name == "ベルトコンベア分岐器", "分岐器が1クリックで置かれる");
    p.Assert(CountBelts() - beltsBeforeSingle == 1, $"分岐器は1個だけ（実際 {CountBelts() - beltsBeforeSingle}）");
    await p.ExitToGameScreen();
    await p.PlaceBlockViaUi("上りベルトコンベア", new Vector3Int(7, 32, -4), BlockDirection.North);
    p.Assert(p.GetBlock(new Vector3Int(7, 32, -4)).BlockMasterElement.Name == "上りベルトコンベア", "上り坂が1クリックで置かれる");
    p.Assert(CountBelts() - beltsBeforeSingle == 2, $"分岐器と上り坂で計2個（実際 {CountBelts() - beltsBeforeSingle}）");
    await p.WaitBlockGameObject(new Vector3Int(7, 32, -4));
    await p.ExitToGameScreen();
    await p.Screenshot("05-single-placement");

    #region Internal

    int CountBelts()
    {
        return ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values.Count(data => BeltConveyorPlaceFamilyUtil.TryGetFamily(data.Block.BlockId, out _));
    }

    int CountOf(IBlock chest, ItemId targetItemId)
    {
        var inventory = chest.GetComponent<IOpenableBlockInventoryComponent>();
        var count = 0;
        for (var i = 0; i < inventory.GetSlotSize(); i++)
            if (inventory.GetItem(i).Id == targetItemId) count += inventory.GetItem(i).Count;
        return count;
    }

    #endregion
});
