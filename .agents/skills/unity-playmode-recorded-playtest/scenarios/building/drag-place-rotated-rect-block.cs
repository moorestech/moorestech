// 長方形ブロック（ふいご付き精錬炉 3x3x7）を東向きにしてドラッグ連続設置し、X・Z どちらの軸でも隙間なく並ぶことを確かめる
// 東向きでは占有が x=7・z=3 に入れ替わるので、X 列は原点 0,7,14、Z 列は原点 10,13,16 に並ぶのが正しい
// Drag-place a rectangular block (bellows smelter 3x3x7) facing east and verify it packs without gaps on both X and Z
// Facing east swaps the footprint to x=7, z=3, so the X run should land at origins 0,7,14 and the Z run at 10,13,16
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

const string BlockName = "ふいご付き精錬炉";
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("drag-place-rotated-rect-block", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(8f, 33.5f, -14f) });
    // Skip受理後もStoryに残るレース（bd moorestech-yx4if.1）を避けるため、飛ばせる間は飛ばし直してからGameScreenを待つ
    // Re-skip while skip is allowed, then wait for GameScreen, to dodge the race that leaves UIState in Story (bd moorestech-yx4if.1)
    await p.SkipOpeningSkitIfPlaying();
    await p.WaitUiState(UIStateEnum.GameScreen, 30f);

    // ---------------------------------------------------------------- 1. 東向きにして X 方向へドラッグ
    p.Note("フェーズ1: 東向き（R 1回）で X 方向へドラッグ → 原点 x=0,7,14 に隙間なく並ぶ");
    await p.OpenBuildMenuAndSelectBlock(BlockName);
    await p.PressKey(Key.R);
    await PlaytestUiOps.DragPlace(Aim(new Vector3Int(0, 32, 0)), Aim(new Vector3Int(14, 32, 0)));
    await p.Until(() => p.GetBlock(new Vector3Int(14, 32, 0)) != null, 15f, "X 列の終端まで設置される");
    await p.WaitSeconds(1f);
    var xOrigins = OriginsIn(new Vector3Int(-2, 32, -2), new Vector3Int(22, 32, 4));
    p.Note($"X 列の原点: {string.Join(", ", xOrigins)}");
    p.Assert(xOrigins.SequenceEqual(new[] { new Vector3Int(0, 32, 0), new Vector3Int(7, 32, 0), new Vector3Int(14, 32, 0) }), "X 列は原点 x=0,7,14 の3台が隙間なく並ぶ");

    // ---------------------------------------------------------------- 2. 同じ東向きのまま Z 方向へドラッグ
    p.Note("フェーズ2: 東向きのまま Z 方向へドラッグ → 原点 z=10,13,16 に隙間なく並ぶ");
    await PlaytestUiOps.DragPlace(Aim(new Vector3Int(0, 32, 10)), Aim(new Vector3Int(0, 32, 16)));
    await p.Until(() => p.GetBlock(new Vector3Int(0, 32, 16)) != null, 15f, "Z 列の終端まで設置される");
    await p.WaitSeconds(1f);
    var zOrigins = OriginsIn(new Vector3Int(-2, 32, 8), new Vector3Int(10, 32, 22));
    p.Note($"Z 列の原点: {string.Join(", ", zOrigins)}");
    p.Assert(zOrigins.SequenceEqual(new[] { new Vector3Int(0, 32, 10), new Vector3Int(0, 32, 13), new Vector3Int(0, 32, 16) }), "Z 列は原点 z=10,13,16 の3台が隙間なく並ぶ");

    var allEast = OriginsIn(new Vector3Int(-2, 32, -2), new Vector3Int(22, 32, 22)).All(origin => p.GetBlock(origin).BlockPositionInfo.BlockDirection == BlockDirection.East);
    p.Assert(allEast, "設置された精錬炉はすべて東向き");

    await p.ExitToGameScreen();
    p.WarpPlayer(new Vector3(10f, 45f, -10f));
    await p.WaitSeconds(1f);
    await p.AimAt(new Vector3(8f, 32f, 9f));
    await p.WaitSeconds(1f);
    await p.Screenshot("01-east-rect-runs");

    #region Internal

    Vector3 Aim(Vector3Int origin)
    {
        return PlaytestUiOps.PlaceAimPoint(BlockName, origin, BlockDirection.East);
    }

    // 範囲内の各セルを占めるブロックの原点を重複なく並べる
    // Collects the distinct origins of blocks occupying cells in the range
    List<Vector3Int> OriginsIn(Vector3Int min, Vector3Int max)
    {
        var origins = new SortedSet<(int, int, int)>();
        for (var x = min.x; x <= max.x; x++)
        for (var z = min.z; z <= max.z; z++)
        {
            var block = p.GetBlock(new Vector3Int(x, min.y, z));
            if (block == null) continue;
            var o = block.BlockPositionInfo.OriginalPos;
            origins.Add((o.x, o.y, o.z));
        }
        return origins.Select(o => new Vector3Int(o.Item1, o.Item2, o.Item3)).ToList();
    }

    #endregion
});
