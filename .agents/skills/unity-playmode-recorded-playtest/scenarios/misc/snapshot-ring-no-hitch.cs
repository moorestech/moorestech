// スナップショットリング有効時にtickを取りこぼさないことと、取り込み時間を実測する（ADR 0057 の初版条件）
// Measures that the snapshot ring drops no ticks and how long a capture takes (ADR 0057 v1 condition)
using System;
using System.Diagnostics;
using System.Threading;
using Client.Playtest;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using UniRx;
using UnityEngine;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 380f };

return PlaytestRunner.Run("snapshot-ring-no-hitch", options, async p =>
{
    await p.WaitSeconds(3f);
    var assembler = ServerContext.GetService<AssembleSaveJsonText>();

    // tickへ差し込む購読口・末尾リストは無いので、取り込み時間はシナリオ側からCapture()を直接呼んで測る
    // There is no tick hook or tick-end list to inject into, so capture time is measured by calling Capture() directly from the scenario
    // Capture()はtickスレッド専用の契約なので、tick末尾で呼んでその中で時間を測る
    // Capture() is contracted to the tick thread, so call it at the tick end and time it there

    // 取り込み時間のブロック数依存を見るため、素のワールド→土台のみ→チェスト込みの3段で測る
    // Measure captures at three scales (bare world, foundations only, plus chests) to expose the per-block dependency
    await MeasureCaptures("loaded-world");

    const int foundationCount = 7048;
    const int chestCount = 1000;
    var placeWall = Stopwatch.StartNew();
    for (var i = 0; i < foundationCount; i++)
    {
        await p.PlaceBlockDirect("基本土台", new Vector3Int(200 + i % 85 * 5, 40, 200 + i / 85 * 5), BlockDirection.North);
        if (i % 500 == 499) await UniTask.Yield();
    }
    p.Note($"foundations placed blocks={ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count}");
    await MeasureCaptures("foundations-only");

    for (var i = 0; i < chestCount; i++)
    {
        await p.PlaceBlockDirect("木のチェスト", new Vector3Int(900 + i % 40 * 2, 40, 900 + i / 40 * 2), BlockDirection.North);
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

    // 書き出し完了はtickスレッドから通知されるので、開始時の基準取り込みを含む成功数を原子的に数える
    // Write completions arrive on the tick thread, so successes including the start baseline are counted atomically
    var ring = ServerContext.GetService<WorldSnapshotRing>();
    var writtenSnapshotCount = 0;
    var writtenSubscription = ring.OnSnapshotWritten.Subscribe(written =>
    {
        if (written.Success) Interlocked.Increment(ref writtenSnapshotCount);
    });
    await p.RunOnServer("常時記録リングを開始する", () => ring.Start(null, null, null));
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
    var writtenSnapshots = Volatile.Read(ref writtenSnapshotCount);
    p.Note($"ticks elapsed={elapsedTicks} expected={expectedTicks:F1} dropped={dropped:F1} ringAttributed={ringAttributedDropped:F1} snapshots={writtenSnapshots}");
    p.Assert(3 <= writtenSnapshots, "開始時の基準＋90秒の周期で3回以上スナップショットが書かれた");
    p.Assert(dropped < 3d, $"取りこぼしが3tick未満 (dropped {dropped:F1})");
    p.Assert(ringAttributedDropped < 3d, $"リング起因の取りこぼしが3tick未満 (ringAttributed {ringAttributedDropped:F1})");

    writtenSubscription.Dispose();

    #region Internal

    async UniTask<double> MeasureCaptures(string label)
    {
        var worstMilliseconds = 0d;
        for (var i = 0; i < 5; i++)
        {
            var milliseconds = 0d;
            await p.RunOnServer($"取り込み時間を測る {label}#{i}", () =>
            {
                var captureWatch = Stopwatch.StartNew();
                assembler.Capture();
                milliseconds = captureWatch.Elapsed.TotalMilliseconds;
            });
            if (worstMilliseconds < milliseconds) worstMilliseconds = milliseconds;
            p.Note($"capture[{label}]#{i}: {milliseconds:F1}ms");
            await p.WaitSeconds(0.5f);
        }
        return worstMilliseconds;
    }

    #endregion
});
