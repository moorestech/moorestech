// スナップショットリング有効時にtickを取りこぼさないことと、取り込み時間を実測する（ADR 0057 の初版条件）
// Measures that the snapshot ring drops no ticks and how long a capture takes (ADR 0057 v1 condition)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Client.Playtest;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using UnityEngine;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 380f };

return PlaytestRunner.Run("snapshot-ring-no-hitch", options, async p =>
{
    await p.WaitSeconds(3f);
    var assembler = ServerContext.GetService<AssembleSaveJsonText>();

    // Capture()はtickスレッド専用の契約なので、計測もtick末尾フックの中で行う（この計測専用フックはシナリオ終了時に外す）
    // Capture() is contracted to the tick thread, so timing happens inside a tick-end hook (this measurement-only hook is removed at the end)
    var probeLock = new object();
    var probeRequested = false;
    var probeMilliseconds = -1d;
    Action captureProbe = () =>
    {
        lock (probeLock)
        {
            if (!probeRequested) return;
            var probeWatch = Stopwatch.StartNew();
            assembler.Capture();
            probeMilliseconds = probeWatch.Elapsed.TotalMilliseconds;
            probeRequested = false;
        }
    };

    // 走行中のtickスレッドが列挙している最中に足すと列挙例外になるため、新しいリストへ差し替える
    // Adding while the running tick thread enumerates would throw, so the list reference is swapped instead
    GameUpdater.FinalTickEndUpdates = new List<Action>(GameUpdater.FinalTickEndUpdates) { captureProbe };

    // 取り込み時間のブロック数依存を見るため、素のワールド→土台のみ→チェスト込みの3段で測る
    // Measure captures at three scales (bare world, foundations only, plus chests) to expose the per-block dependency
    await MeasureCaptures("loaded-world");

    const int foundationCount = 7048;
    const int chestCount = 1000;
    var placeWall = Stopwatch.StartNew();
    for (var i = 0; i < foundationCount; i++)
    {
        p.PlaceBlockDirect("基本土台", new Vector3Int(200 + i % 85 * 5, 40, 200 + i / 85 * 5), BlockDirection.North);
        if (i % 500 == 499) await UniTask.Yield();
    }
    p.Note($"foundations placed blocks={ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count}");
    await MeasureCaptures("foundations-only");

    for (var i = 0; i < chestCount; i++)
    {
        p.PlaceBlockDirect("木のチェスト", new Vector3Int(900 + i % 40 * 2, 40, 900 + i / 40 * 2), BlockDirection.North);
        if (i % 500 == 499) await UniTask.Yield();
    }
    var blockCount = ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count;
    p.Note($"blocks={blockCount} placed in {placeWall.Elapsed.TotalSeconds:F1}s");

    // リングを止めたまま進行を測り、この環境そのものが持つtick遅れを基準値にする
    // Measure progress with the ring stopped to get this environment's own tick lag as a baseline
    var baselineStartTick = GameUpdater.CurrentTick;
    var baselineWall = Stopwatch.StartNew();
    await p.WaitSeconds(30f);
    var baselineElapsedTicks = (double)(GameUpdater.CurrentTick - baselineStartTick);
    var baselineExpectedTicks = baselineWall.Elapsed.TotalSeconds * GameUpdater.TicksPerSecond;
    var baselineDropped = baselineExpectedTicks - baselineElapsedTicks;
    p.Note($"baseline(ring off) elapsed={baselineElapsedTicks} expected={baselineExpectedTicks:F1} dropped={baselineDropped:F1}");

    var ring = ServerContext.GetService<WorldSnapshotRing>();
    ring.Start(SnapshotRingConfig.PeriodTicks, SnapshotRingConfig.Generations);
    p.Note("ring started");
    var worst = await MeasureCaptures("ring-on");
    p.Assert(worst <= 20d, $"取り込みが20ms以内 (worst {worst:F1}ms)");

    // 90秒で3回の周期スナップショットを跨ぎ、tickの取りこぼしを数える
    // Span three periodic snapshots over 90 seconds and count dropped ticks
    var startTick = GameUpdater.CurrentTick;
    var wall = Stopwatch.StartNew();
    await p.WaitSeconds(90f);
    var elapsedTicks = (double)(GameUpdater.CurrentTick - startTick);
    var expectedTicks = wall.Elapsed.TotalSeconds * GameUpdater.TicksPerSecond;
    var dropped = expectedTicks - elapsedTicks;
    var ringAttributedDropped = dropped - baselineDropped * (expectedTicks / baselineExpectedTicks);
    p.Note($"ticks elapsed={elapsedTicks} expected={expectedTicks:F1} dropped={dropped:F1} ringAttributed={ringAttributedDropped:F1} snapshots={ring.CopyWrittenTicks().Count}");
    p.Assert(ring.CopyWrittenTicks().Count >= 3, "90秒で3回以上スナップショットが書かれた");
    p.Assert(dropped < 3d, $"取りこぼしが3tick未満 (dropped {dropped:F1})");
    p.Assert(ringAttributedDropped < 3d, $"リング起因の取りこぼしが3tick未満 (ringAttributed {ringAttributedDropped:F1})");

    var withoutProbe = new List<Action>(GameUpdater.FinalTickEndUpdates);
    withoutProbe.Remove(captureProbe);
    GameUpdater.FinalTickEndUpdates = withoutProbe;

    #region Internal

    async UniTask<double> MeasureCaptures(string label)
    {
        var worstMilliseconds = 0d;
        for (var i = 0; i < 5; i++)
        {
            var milliseconds = await MeasureOneCaptureOnTickThread();
            if (worstMilliseconds < milliseconds) worstMilliseconds = milliseconds;
            p.Note($"capture[{label}]#{i}: {milliseconds:F1}ms");
            await p.WaitSeconds(0.5f);
        }
        return worstMilliseconds;
    }

    async UniTask<double> MeasureOneCaptureOnTickThread()
    {
        lock (probeLock)
        {
            probeMilliseconds = -1d;
            probeRequested = true;
        }

        for (var waited = 0; waited < 100; waited++)
        {
            await p.WaitSeconds(0.1f);
            lock (probeLock)
            {
                if (probeMilliseconds >= 0d) return probeMilliseconds;
            }
        }

        // 10秒待ってもtick末尾フックが走らないのはtickループ停止なので、黙って0msを返さず失敗として残す
        // If the tick-end hook has not run in 10 seconds the tick loop is stopped, so fail loudly instead of reporting 0ms
        p.Assert(false, "計測用フックがtickスレッドで実行されなかった");
        return 0d;
    }

    #endregion
});
