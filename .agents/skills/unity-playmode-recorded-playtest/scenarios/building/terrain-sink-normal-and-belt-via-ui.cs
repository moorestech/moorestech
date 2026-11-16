// 生成ワールドの端数地形・斜面で、通常設置とベルトの地形めり込み判定（ADR 0081）をUI経路で確かめる
// 通常設置: オフセット0で置ける → Qで1段下げると赤＋「地形に埋まっています」、クリックしても置かれない
// ベルト: オフセット0のドラッグは全セル青で置ける → Qで1段下げたドラッグは全セル赤で置かれない
// 起動は PLAYTEST_WORLD_DIRECTORY(毎回未作成のパス)/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED を付ける
// Checks the terrain-sink gate (ADR 0081) for normal placement and belts on fractional, sloped generated terrain via the UI.
// Normal: offset 0 places; one step down with Q turns red with the terrain reason and a click places nothing.
// Belt: an offset-0 drag is all blue and placed; a drag one step down is all red and places nothing.
// Boot with PLAYTEST_WORLD_DIRECTORY (fresh path)/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED.
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("terrain-sink-normal-and-belt-via-ui", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable
    var inputSettings = InputSystem.settings;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    // ツールチップのWeb UI描画をDOMで確かめるため、照会ハンドラを先に登録する
    // Register the DOM query handler first so the tooltip's Web UI rendering can be checked
    Client.Playtest.UiElements.PlaytestUiLocatorSession.Start();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var heightOffset = resolver.Resolve<PlacementHeightOffset>();
    var tooltip = resolver.Resolve<IMouseCursorTooltip>();
    const string TerrainKey = "ui.tooltip.placeBlockedByTerrain";

    // 足場(原点±25)の外で、16セルで2〜7上る遮蔽物の無い斜面を探す
    // Find a clear 16-cell slope rising 2-7 outside the scaffold (origin ±25)
    p.Assert(PlaytestTerrainSurvey.TryFindRisingRunAlongX(new Vector2Int(0, 40), 120, 16, 2f, 7f, 32, out var run), "足場の外に上り斜面がある");
    p.Note($"斜面: {run.Describe()}");
    p.WarpPlayer(new Vector3(run.StartCell.x + 7.5f, run.Heights[7] + 2f, run.StartCell.y - 6f));
    await p.WaitSeconds(1.5f);

    await p.Hotbar.AssignHotbar(0, "木のコンベアチェスト");
    await p.Hotbar.AssignHotbar(1, "ベルトコンベア");

    // ---- 1. 通常設置: オフセット0で置ける
    // ---- 1. Normal placement: offset 0 places
    // 高さ2のブロックにして、1段沈めても上半分が地上に出て赤が見えるようにする
    // A two-high block keeps its upper half above ground when sunk one step, so the red stays visible
    p.Note("1. 木のコンベアチェスト(3x2x4)を斜面(i=0)へ高さオフセット0で置く");
    await p.Hotbar.EnterBuildMode(0);
    var chestSize = BlockSize("木のコンベアチェスト");
    var cellA = Cell(0);
    var defaultA = DefaultCellY(cellA, chestSize);
    p.Note($"i=0 地表={run.Heights[0]:F3} 既定Y={defaultA}");
    await AimFootprint(cellA, chestSize);
    var previewA = PlacementPreviewProbe.ActiveBlockPreviews();
    p.Assert(previewA.Count == 1 && Mathf.RoundToInt(previewA[0].Position.y) == defaultA, $"プレビューは既定Y={defaultA} 実際:{string.Join(";", previewA.Select(s => s.Position))}");
    p.Assert(previewA.Count == 1 && previewA[0].IsPlaceable, $"オフセット0のプレビューは青 実際:{(previewA.Count > 0 ? previewA[0].Color.ToString() : "-")}");
    p.Assert(!HasTooltipKey(TerrainKey), $"オフセット0では地形理由が出ない tooltip={TooltipText()}");
    await p.Screenshot("01-chest-offset0-blue");
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(cellA.x, defaultA, cellA.y)) != null, 15f, $"チェストが既定Y({cellA.x},{defaultA},{cellA.y})に置かれる");
    await p.WaitBlockGameObject(new Vector3Int(cellA.x, defaultA, cellA.y));

    // ---- 1b. Qで1段下げると赤＋地形理由、クリックしても置かれない
    // ---- 1b. One step down with Q: red, terrain reason, and the click places nothing
    p.Note("1b. Qで1段下げて斜面(i=4)を狙う → 赤＋「地形に埋まっています」");
    await p.PressKey(Key.Q);
    p.Assert(heightOffset.Value == -1, $"Qで高さオフセット-1 実際:{heightOffset.Value}");
    var cellB = Cell(4);
    var sunkB = DefaultCellY(cellB, chestSize) - 1;
    await AimFootprint(cellB, chestSize);
    var previewB = PlacementPreviewProbe.ActiveBlockPreviews();
    p.Note($"i=4 地表={run.Heights[4]:F3} 沈めたY={sunkB} tooltip={TooltipText()}");
    p.Assert(previewB.Count == 1 && Mathf.RoundToInt(previewB[0].Position.y) == sunkB, $"プレビューは1段下のY={sunkB} 実際:{string.Join(";", previewB.Select(s => s.Position))}");
    p.Assert(previewB.Count == 1 && previewB[0].IsNotPlaceable, $"1段下げたプレビューは赤 実際:{(previewB.Count > 0 ? previewB[0].Color.ToString() : "-")}");
    p.Assert(HasTooltipKey(TerrainKey), $"「地形に埋まっています」が出る tooltip={TooltipText()}");
    var tooltipDom = await PlaytestUiElementOps.FindUi("cursor-tooltip");
    p.Assert(tooltipDom.IsFound && (tooltipDom.Text.Contains("地形に埋まっています") || tooltipDom.Text.Contains("Blocked by terrain")), $"Web UIのツールチップに「地形に埋まっています」が描画される 実際:found={tooltipDom.IsFound} text={tooltipDom.Text}");
    await p.Screenshot("02-chest-sunk-red");
    await p.ClickPlace();
    await p.WaitSeconds(1.5f);
    p.Assert(!AnyBlockInColumn(cellB, sunkB - 1, sunkB + 2), $"1段下げたクリックでは何も置かれない ({cellB.x},{sunkB},{cellB.y})");
    await p.Hotbar.ExitBuildMode(0);

    // ---- 3. ベルト: オフセット0のドラッグは、地中に入らないセルが青で置かれ、地中セルだけ赤で置かれない
    // ---- 3. Belt: in an offset-0 drag, unsunk cells are blue and placed, and only sunk cells are red and skipped
    // ベルトの列Yは始点と終点の間を直線補間し地形に沿わない（地形追従は bd moorestech-5wy86 の範囲）ため、斜面の中間セルは地中に入りうる
    // A belt run interpolates Y between its ends and does not follow terrain (terrain following is bd moorestech-5wy86), so mid-slope cells can sink
    p.Note("3. ベルトを斜面 i=8→10 へオフセット0でドラッグ → 色は各セルの埋まり判定どおり、置かれるのは青だけ");
    await p.Hotbar.EnterBuildMode(1);
    p.Assert(heightOffset.Value == 0, $"持ち替えで高さは0に戻る 実際:{heightOffset.Value}");
    var beltPreview0 = await DragAndRead(8, 10, "03-belt-offset0-dragging");
    p.Note($"既定Y:[{string.Join(",", beltPreview0.Select(s => DefaultCellY(PreviewCell(s), Vector3Int.one)))}]");
    p.Assert(beltPreview0.Count == 3 && beltPreview0.All(s => s.IsPlaceable == !IsSunkCell(s)), $"各セルの色が埋まり判定どおり（埋まり=赤・それ以外=青） 実際:{Describe(beltPreview0)}");
    p.Assert(beltPreview0.Any(s => s.IsPlaceable), "オフセット0のドラッグには置けるセルがある");
    await p.Until(() => beltPreview0.Where(s => s.IsPlaceable).All(s => p.GetBlock(BlockCell(s)) != null), 15f, "青いセルのベルトが置かれる");
    await p.WaitSeconds(1f);
    p.Assert(beltPreview0.Where(s => s.IsNotPlaceable).All(s => p.GetBlock(BlockCell(s)) == null), "赤い（地中）セルのベルトは置かれない");

    // ---- 3b. Qで1段下げたドラッグは全セル赤で置かれない
    // ---- 3b. A drag one step down is all red and places nothing
    p.Note("3b. Qで1段下げて斜面 i=12→14 をドラッグ → 地中セルが赤で置かれない");
    await p.PressKey(Key.Q);
    p.Assert(heightOffset.Value == -1, $"ベルトでもQで-1 実際:{heightOffset.Value}");
    var beltPreview1 = await DragAndRead(12, 14, "04-belt-sunk-dragging");
    p.Assert(beltPreview1.Count == 3 && beltPreview1.All(s => s.IsNotPlaceable), $"1段下げたベルト3セルが赤 実際:{Describe(beltPreview1)}");
    p.Assert(beltPreview1.All(s => Mathf.RoundToInt(s.Position.y) < DefaultCellY(PreviewCell(s), Vector3Int.one)), $"赤いセルはどれも既定Yより下（地中） 既定Y:[{string.Join(",", beltPreview1.Select(s => DefaultCellY(PreviewCell(s), Vector3Int.one)))}]");
    await p.WaitSeconds(1.5f);
    p.Assert(beltPreview1.All(s => !AnyBlockInColumn(PreviewCell(s), Mathf.RoundToInt(s.Position.y) - 1, DefaultCellY(PreviewCell(s), Vector3Int.one) + 1)), "地中に入れたベルトは1つも置かれない");
    await p.Hotbar.ExitBuildMode(1);
    await p.Screenshot("05-result");

    Vector2Int Cell(int index) => new(run.StartCell.x + index, run.StartCell.y);

    // ベルトはX方向へ並ぶのでプレビューのXが列のセル、Zは斜面の行（東向きのモデル原点はZが1ずれる）
    // Belts run along X, so the preview X is the cell and Z is the slope row (an east-facing model origin shifts Z by one)
    Vector2Int PreviewCell(PlacementPreviewSnapshot s) => new(Mathf.RoundToInt(s.Position.x), run.StartCell.y);
    Vector3Int BlockCell(PlacementPreviewSnapshot s) => new(PreviewCell(s).x, Mathf.RoundToInt(s.Position.y), run.StartCell.y);
    bool IsSunkCell(PlacementPreviewSnapshot s) => PlacementTerrainSinkQuery.IsSunk(BlockCell(s), BlockDirection.North, Vector3Int.one);

    Vector3Int BlockSize(string name) => MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId(name)).BlockSize;

    // 埋まらない最も低いセル＝地形が自然に置く既定Y（オフセット0）
    // The lowest unsunk cell is the terrain's natural default Y (offset 0)
    int DefaultCellY(Vector2Int cell, Vector3Int size)
    {
        var y = Mathf.FloorToInt(run.Heights.Min()) - 4;
        while (PlacementTerrainSinkQuery.IsSunk(new Vector3Int(cell.x, y, cell.y), BlockDirection.North, size)) y++;
        return y;
    }

    bool AnyBlockInColumn(Vector2Int cell, int fromY, int toY) => Enumerable.Range(fromY, toY - fromY + 1).Any(y => p.GetBlock(new Vector3Int(cell.x, y, cell.y)) != null);

    async UniTask AimFootprint(Vector2Int origin, Vector3Int size)
    {
        // フットプリント中心の地表を狙うと、その原点に置かれる
        // Aiming at the ground under the footprint center yields that origin
        var centerX = origin.x + size.x / 2f;
        var centerZ = origin.y + size.z / 2f;
        GroundHeightProbe.TryGetGroundPoint(centerX, centerZ, out var ground);
        await p.AimAt(ground);
        await UniTask.DelayFrame(10);
    }

    async UniTask<System.Collections.Generic.List<PlacementPreviewSnapshot>> DragAndRead(int from, int to, string shot)
    {
        // 押下したまま終点へ動かし、離す直前のプレビューを読む
        // Hold the button to the end cell and read the previews right before release
        await p.AimAt(run.SurfacePoint(from));
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(5);
        await p.AimAt(run.SurfacePoint(to));
        await UniTask.DelayFrame(10);
        var previews = PlacementPreviewProbe.ActiveBlockPreviews();
        p.Note($"ドラッグ中 {Describe(previews)} tooltip={TooltipText()}");
        await p.Screenshot(shot);
        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(5);
        return previews;
    }

    string Describe(System.Collections.Generic.List<PlacementPreviewSnapshot> previews) => string.Join(";", previews.Select(s => $"{s.Position}:{(s.IsPlaceable ? "青" : s.IsNotPlaceable ? "赤" : "?")}"));

    bool HasTooltipKey(string key)
    {
        var presentation = tooltip.GetPresentation();
        return presentation.Visible && presentation.Lines.Any(line => line.Key.Key == key);
    }

    string TooltipText()
    {
        var presentation = tooltip.GetPresentation();
        return presentation.Visible ? string.Join(" / ", presentation.Lines.Select(line => line.Key.Key)) : "(hidden)";
    }
});
