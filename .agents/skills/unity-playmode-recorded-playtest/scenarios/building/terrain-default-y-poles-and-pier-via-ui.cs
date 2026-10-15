// 生成ワールドの端数地形・斜面で、歯車チェーンポール・電線ツールの電柱・レール橋脚が既定位置に置けることをUI経路で確かめる（ADR 0081 R3・#1507の退行確認）
// 各設置系は高さオフセットを持たず、地形最高点を含むセルへ置く。その既定Yは地形めり込み判定で不可にならないはず
// 起動は PLAYTEST_WORLD_DIRECTORY(毎回未作成のパス)/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED を付ける
// Verifies via the UI that the gear chain pole, the wire tool's electric pole and the rail pier place at their default Y on fractional sloped generated terrain (ADR 0081 R3, #1507 regression).
// These systems have no height offset and place into the cell holding the terrain max; that default Y must never trip the terrain-sink gate.
// Boot with PLAYTEST_WORLD_DIRECTORY (fresh path)/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED.
using System.Linq;
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

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("terrain-default-y-poles-and-pier-via-ui", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable
    var inputSettings = InputSystem.settings;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    var tooltip = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();

    // 歯車ポールの延長設置と電線ツールは接続ツールの解放状態も見るため先に解放し、電線素材も持たせる
    // Gear pole extension and the wire tool consult connect-tool unlocks, so unlock them first and stock wire
    p.Hotbar.UnlockConnectTool("歯車チェーン");
    p.Hotbar.UnlockConnectTool("電線");
    p.GiveItemDirect("銅のワイヤー", 64);
    p.GiveItemDirect("鉄のワイヤー", 64);
    // 無料設置でも接続系の設置はコスト不足行を出し、サーバーは未解放を拒否する（[GearChainPoleExtend] denied: NotUnlocked）ため、解放と建設コストを実際に持たせる
    // Connect-style placement still reports shortages under free placement and the server denies locked poles, so unlock and stock the real cost
    p.UnlockBlock("歯車チェーンポール");
    p.UnlockBlock("電柱");
    p.UnlockBlock("レール橋脚");
    await p.GiveConstructionCost("歯車チェーンポール", 2);
    await p.GiveConstructionCost("電柱", 2);
    await p.GiveConstructionCost("レール橋脚", 2);
    await p.Hotbar.AssignHotbar(0, "歯車チェーンポール");
    await p.Hotbar.AssignHotbar(1, "電線");
    await p.Hotbar.AssignHotbar(2, "レール橋脚");

    // 足場(原点±25)の外で、24セルで2〜8上る遮蔽物の無い斜面を探す
    // Find a clear 24-cell slope rising 2-8 outside the scaffold (origin ±25)
    p.Assert(PlaytestTerrainSurvey.TryFindRisingRunAlongX(new Vector2Int(0, 40), 120, 24, 2f, 8f, 32, out var run), "足場の外に上り斜面がある");
    p.Note($"斜面: {run.Describe()}");

    p.Note("2a. 歯車チェーンポール(3x4x3)を斜面 i=1 の既定位置へ置く");
    await PlaceAndVerify(0, "歯車チェーンポール", 1, "01-gear-chain-pole");
    p.Note("2b. 電線ツールで電柱を斜面 i=10 の既定位置へ置く");
    await PlaceAndVerify(1, "電柱", 10, "02-electric-pole");
    p.Note("2c. レール橋脚(3x1x3)を斜面 i=18 の既定位置へ置く");
    await PlaceAndVerify(2, "レール橋脚", 18, "03-rail-pier");
    await p.Screenshot("04-all-placed");

    async UniTask PlaceAndVerify(int slot, string blockName, int runIndex, string shot)
    {
        // フットプリント中心の地表を狙ってクリックし、置かれたブロックの原点Yが既定Yと一致し埋まり判定に掛からないことを見る
        // Click the ground under the footprint center, then check the placed origin Y equals the default Y and is not judged sunk
        var size = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId(blockName)).BlockSize;
        var origin = new Vector2Int(run.StartCell.x + runIndex, run.StartCell.y);
        var center = new Vector2(origin.x + size.x / 2f, origin.y + size.z / 2f);
        var expectedY = DefaultCellY(origin, size);
        GroundHeightProbe.TryGetGroundPoint(center.x, center.y, out var ground);
        GroundHeightProbe.TryGetFootprintMaxGroundHeight(new Vector3Int(origin.x, 0, origin.y), BlockDirection.North, size, out var footprintMax, out _);
        p.Note($"{blockName}: 原点({origin.x},{origin.y}) フットプリント地表最高={footprintMax:F3} 既定Y={expectedY}");

        // 照準先が画面左のキー操作ヘルプ（UI）に重なるとクリックが無効になるため、対象の真南へ立って画面中央で狙う
        // A click over the left key-hint UI is ignored, so stand due south of the target and aim at screen center
        p.WarpPlayer(new Vector3(center.x, ground.y + 2f, center.y - 8f));
        await p.WaitSeconds(1f);
        await p.Hotbar.EnterBuildMode(slot);
        await p.AimAt(ground);
        await UniTask.DelayFrame(10);
        p.Note($"{blockName} 照準中 tooltip={TooltipText()}");
        p.Assert(!HasTooltipKey("ui.tooltip.placeBlockedByTerrain"), $"{blockName}: 既定位置で「地形に埋まっています」が出ない tooltip={TooltipText()}");
        await p.Screenshot($"{shot}-ghost");
        await p.ClickPlace();

        var centerCell = new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
        IBlock placed = null;
        await p.Until(() => (placed = FindBlockInColumn(centerCell, expectedY - 3, expectedY + 6)) != null, 15f, $"{blockName} がサーバーに置かれる");
        var info = placed.BlockPositionInfo;
        var placedOrigin = info.OriginalPos;
        var placedDefaultY = DefaultCellY(new Vector2Int(placedOrigin.x, placedOrigin.z), size);
        p.Note($"{blockName} 設置: 名前={placed.BlockMasterElement.Name} 原点={placedOrigin} 向き={info.BlockDirection} その列の既定Y={placedDefaultY}");
        p.Assert(placed.BlockMasterElement.Name == blockName, $"置かれたのは{blockName} 実際:{placed.BlockMasterElement.Name}");
        p.Assert(placedOrigin.y == placedDefaultY, $"{blockName}: 原点Yが既定Y={placedDefaultY} 実際:{placedOrigin.y}");
        p.Assert(!PlacementTerrainSinkQuery.IsSunk(placedOrigin, info.BlockDirection, size), $"{blockName}: 置かれた位置は地形めり込み判定で不可にならない");
        await p.WaitBlockGameObject(placedOrigin);
        await p.Hotbar.ExitBuildMode(slot);
        await p.Screenshot($"{shot}-placed");
    }

    // 埋まらない最も低いセル＝地形が自然に置く既定Y（オフセット0）
    // The lowest unsunk cell is the terrain's natural default Y (offset 0)
    int DefaultCellY(Vector2Int cell, Vector3Int size)
    {
        var y = Mathf.FloorToInt(run.Heights.Min()) - 4;
        while (PlacementTerrainSinkQuery.IsSunk(new Vector3Int(cell.x, y, cell.y), BlockDirection.North, size)) y++;
        return y;
    }

    IBlock FindBlockInColumn(Vector2Int cell, int fromY, int toY)
    {
        for (var y = fromY; y <= toY; y++)
        {
            var block = p.GetBlock(new Vector3Int(cell.x, y, cell.y));
            if (block != null) return block;
        }
        return null;
    }

    bool HasTooltipKey(string key)
    {
        var presentation = tooltip.GetPresentation();
        return presentation.Visible && presentation.Lines.Any(line => line.Key.Key == key);
    }

    string TooltipText()
    {
        var presentation = tooltip.GetPresentation();
        return presentation.Visible ? string.Join(" / ", presentation.Lines.Select(line => line.TextParams.Count == 0 ? line.Key.Key : line.Key.Key + "[" + string.Join(",", line.TextParams) + "]")) : "(hidden)";
    }
});
