using System;
using System.Collections.Generic;
using System.IO;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Server.Boot.Replay
{
    // スナップショットをロードし、記録済みパケットを同じ処理tickへ流し直して目標tickまで進める
    // Loads a snapshot, feeds recorded packets back at their original processing ticks, and advances to the target tick
    public static class SnapshotReplayer
    {
        // 再生はプロセス全体の静的状態（ServerContext・MasterHolder・GameUpdater）を差し替えるため、稼働中サーバーと同一プロセスで呼んではいけない
        // Replay swaps process-wide statics (ServerContext, MasterHolder, GameUpdater), so it must never run in the same process as a live server
        public static ReplayResult Replay(ReplayRequest request)
        {
            Debug.LogWarning("再生はプロセス全体の静的状態（ServerContext・MasterHolder・GameUpdater）を差し替えます。稼働中サーバーと同一プロセスで呼ばないこと");

            // ロード経路は通常起動と同じ（save.jsonの位置にスナップショットを置く）。常時記録は開始しない
            // Same load path as a normal boot (the snapshot sits where save.json would); capture stays off
            var tempRoot = Path.Combine(Path.GetTempPath(), $"moorestech-replay-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            var savePath = Path.Combine(tempRoot, "save.json");
            File.Copy(request.SnapshotFilePath, savePath);

            var options = new MoorestechServerDIContainerOptions(request.ServerDataDirectory)
            {
                // マップ・terrainは記録時と同じワールドから読む。template を読むとマップオブジェクトの instanceId が丸ごとずれる
                // Map and terrain come from the recording's own world; reading the template shifts every map-object instance id
                worldDataDirectory = request.SourceWorld.WithSaveJsonFilePath(savePath),
            };
            var (packetResponseCreator, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loadedTick = GameUpdater.CurrentTick;

            // 目標tickがスナップショットより前なら再生は成立しない。無言で loadedTick を返すと呼び出し側が成功と誤読する
            // A target before the snapshot cannot be replayed; silently returning loadedTick would read as success to the caller
            if (request.TargetTick < loadedTick)
            {
                Debug.LogError($"再生を拒否 目標tickがスナップショットより前 loaded:{loadedTick} target:{request.TargetTick} snapshot:{request.SnapshotFilePath}");
                Directory.Delete(tempRoot, true);
                throw new InvalidOperationException($"目標tick {request.TargetTick} はスナップショットのtick {loadedTick} より前です");
            }

            var records = ReceivedPacketLogReader.ReadAll(request.PacketLogFilePaths);
            var inRange = ReportPacketLogCoverage(records, loadedTick, request.TargetTick);
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            var context = new PacketResponseContext(null);
            var replayed = 0;
            var excluded = 0;
            var next = 0;

            // 記録tick == 次のtick のパケットを積んでから Update する。tick末尾でまとめて処理される
            // Enqueue packets whose recorded tick equals the next tick, then Update; they are processed together at tick end
            while (GameUpdater.CurrentTick < request.TargetTick)
            {
                var nextTick = GameUpdater.CurrentTick + 1;
                while (next < records.Count && records[next].Tick < nextTick) next++;
                while (next < records.Count && records[next].Tick == nextTick)
                {
                    if (IsExcludedFromReplay(records[next].Payload)) excluded++;
                    else
                    {
                        queue.Enqueue(new ReplayPacketEntry(packetResponseCreator, context, records[next].Payload));
                        replayed++;
                    }
                    next++;
                }
                GameUpdater.Update();
            }

            var json = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            Debug.Log($"再生完了 loaded:{loadedTick} reached:{GameUpdater.CurrentTick} packets:{replayed} 除外:{excluded}");
            Directory.Delete(tempRoot, true);
            return new ReplayResult(loadedTick, GameUpdater.CurrentTick, replayed, inRange, excluded, json);

            #region Internal

            // 渡されたログが再生区間をどれだけ覆っているかを必ず出す。0件再生を「一致しなかった＝非決定性」と誤読させないため
            // Always report how much of the replay interval the given log covers, so a zero-packet replay is not misread as non-determinism
            // セーブと即時取得は再生対象から外す。走らせると再生用の一時セーブを上書きし、常時記録まで動き出す
            // Saves and immediate captures are excluded: running them overwrites the temporary save and even starts always-on capture
            bool IsExcludedFromReplay(byte[] payload)
            {
                var tag = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(payload).Tag;
                return tag == SaveProtocol.ProtocolTag || tag == BugReportCaptureProtocol.ProtocolTag;
            }

            int ReportPacketLogCoverage(IReadOnlyList<ReceivedPacketRecord> coverageRecords, ulong coverageLoadedTick, ulong coverageTargetTick)
            {
                var beforeSnapshot = 0;
                var inRange = 0;
                foreach (var record in coverageRecords)
                {
                    if (record.Tick <= coverageLoadedTick) beforeSnapshot++;
                    else if (record.Tick <= coverageTargetTick) inRange++;
                }

                Debug.Log($"再生対象パケット 区間({coverageLoadedTick},{coverageTargetTick}]:{inRange}件 スナップショット以前で読み飛ばし:{beforeSnapshot}件 ログ全件:{coverageRecords.Count}件");
                if (inRange == 0) Debug.LogWarning($"パケットログが区間({coverageLoadedTick},{coverageTargetTick}]を1件も含んでいません。ローテーションで消えたか別セグメントを渡した可能性があります");
                return inRange;
            }

            #endregion
        }
    }
}
