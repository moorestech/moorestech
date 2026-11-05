// ベルトの2クリック設置（ADR 0097）の通し検証。平地のL字、チェストの壁の乗り越え、Cキーの同コスト経路切り替え、
// 置いた経路で荷物が実際に流れることを、実プレイヤーと同じキーマウス経路で確かめる
// End-to-end check of two-click belt placement (ADR 0097): an L on flat ground, climbing over a wall of chests, switching
// equal-cost routes with C, and items actually flowing along the placed route, all through the real key/mouse path
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
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
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-route-two-click-via-ui", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(5f, 33.5f, -3f) });
    await p.SkipOpeningSkit();
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    // 足場は地形の山の中にあり、そのままでは設置物が録画に写らない
    // The scaffold sits inside a terrain mound, so placed blocks would not show on the recording
    p.HideMapObjects();
    // 機能紹介の録画なのでログ欄と入力欄は隠し、カーソルだけ残す
    // Hide the log and input panels for feature footage, keeping only the cursor
    p.SetOverlayPanels(false, false);

    // ---------------------------------------------------------------- 1. 平地のL字
    p.Note("フェーズ1: 始点(2,2)北向き→終点(8,8)東向きの2クリックで最短経路を置く");
    await p.RoutePlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), BlockDirection.North, new Vector3Int(8, 32, 8), BlockDirection.East);
    p.Assert(ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(8, 32, 8)) == BlockDirection.East, "終点は東向き");
    p.Assert(CountBelts() == 13, $"始点から終点までの最短は13セル（実際 {CountBelts()}）");
    // ベルトの見た目は非同期に読み込まれるので、録画に写るまで終点の実体を待つ
    // Belt visuals load asynchronously, so wait for the end cell's object before it is filmed
    await p.WaitBlockGameObject(new Vector3Int(8, 32, 8));
    await p.ExitToGameScreen();
    await p.Screenshot("01-l-route");

    // ---------------------------------------------------------------- 2. 壁を越える
    p.Note("フェーズ2: チェスト3個の壁を越えて東へ。上り→水平→下りを探索が選ぶ");
    // フェーズ1のベルト（z=8）越しに狙うと光線が先に当たるため、壁のすぐ手前に立つ
    // Aiming across the phase-1 belts at z=8 makes the ray hit them first, so stand just short of the wall
    p.WarpPlayer(new Vector3(5f, 33.5f, 10f));
    await p.WaitSeconds(0.5f);
    for (var x = 4; x <= 6; x++) await p.PlaceBlockDirect("木のチェスト", new Vector3Int(x, 32, 12), BlockDirection.North);
    await p.RoutePlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 12), BlockDirection.East, new Vector3Int(9, 32, 12), BlockDirection.East);
    p.Assert(Enumerable.Range(4, 3).All(x => p.GetBlock(new Vector3Int(x, 33, 12)) != null), "壁の上 y=33 にベルトが載る");
    p.Assert(HasBlockNamed("上りベルトコンベア") && HasBlockNamed("下りベルトコンベア"), "上りと下りの坂が使われる");
    await p.WaitBlockGameObject(new Vector3Int(9, 32, 12));
    await p.ExitToGameScreen();
    await p.Screenshot("02-over-wall");

    // ---------------------------------------------------------------- 3. Cで別経路
    p.Note("フェーズ3: 始点を決めてからCを押すと同コストの別経路へ切り替わる");
    p.WarpPlayer(new Vector3(14f, 33.5f, -3f));
    await p.WaitSeconds(0.5f);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(12, 32, 2), BlockDirection.North));
    await p.PressKey(Key.R);
    await p.ClickPlace();

    // 遠い終点を何か所か指して、経路探索中の最悪フレーム時間を実機で測る
    // Sweep a few far targets while routing to measure the worst frame time on the real build
    var worstFrameMs = 0f;
    foreach (var target in new[] { new Vector3Int(17, 32, 9), new Vector3Int(12, 32, 9), new Vector3Int(18, 32, 1), new Vector3Int(17, 32, 7) })
    {
        await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", target, BlockDirection.North));
        for (var frame = 0; frame < 3; frame++)
        {
            await UniTask.Yield();
            worstFrameMs = Mathf.Max(worstFrameMs, Time.unscaledDeltaTime * 1000f);
        }
    }
    p.Note($"経路探索中の最悪フレーム時間: {worstFrameMs:F1} ms");

    var before = PreviewPositions();
    await p.Screenshot("03-route-a");
    await p.PressKey(Key.C);
    var after = PreviewPositions();
    p.Assert(before.Count == after.Count && before.Count == 11, $"同コスト経路はセル数が同じ（{before.Count} / {after.Count}）");
    p.Assert(!before.SequenceEqual(after), "Cで経路の形が変わる");
    await p.Screenshot("04-route-b");
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(17, 32, 7)) != null, 15f, "Cで切り替えた経路が置かれる");
    await p.WaitBlockGameObject(new Vector3Int(17, 32, 7));
    await p.ExitToGameScreen();

    // ---------------------------------------------------------------- 4. 流れる
    p.Note("フェーズ4: L字の始点手前にミニチェストを置いて荷物を流し、終点の先のチェストへ届く");
    p.WarpPlayer(new Vector3(5f, 33.5f, -3f));
    var source = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(2, 32, 1), BlockDirection.North);
    var sink = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(9, 32, 8), BlockDirection.East);
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    await p.RunOnServer("始点手前のチェストへ鉄インゴット5個", () => source.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 5)));
    await p.Until(() => 1 <= CountOf(sink, itemId), 60f, "終点の先のチェストへ荷物が届く");
    await p.Screenshot("05-flow");

    #region Internal

    int CountBelts()
    {
        return ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values.Count(data => BeltConveyorPlaceFamilyUtil.TryGetFamily(data.Block.BlockId, out _));
    }

    bool HasBlockNamed(string name)
    {
        return ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values.Any(data => data.Block.BlockMasterElement.Name == name);
    }

    // 本番のプレビュー実体の位置を読み取り専用で観測する（検証用APIを増やさない）
    // Observe the production preview objects' positions read-only without adding a testing API
    List<Vector3Int> PreviewPositions()
    {
        var controller = Object.FindFirstObjectByType<PlacementPreviewBlockGameObjectController>();
        var field = typeof(PlacementPreviewBlockGameObjectController).GetField("_activePreviewBlocks", BindingFlags.Instance | BindingFlags.NonPublic);
        var blocks = (List<BlockPreviewObject>)field.GetValue(controller);
        return blocks.Select(block => Vector3Int.RoundToInt(block.transform.position)).ToList();
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
