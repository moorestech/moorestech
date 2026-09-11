using System;
using System.IO;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Replay
{
    // スナップショットをロードし、記録済みパケットを同じ処理tickへ流し直して目標tickまで進める
    // Loads a snapshot, feeds recorded packets back at their original processing ticks, and advances to the target tick
    public static class SnapshotReplayer
    {
        public static ReplayResult Replay(ReplayRequest request)
        {
            // ロード経路は通常起動と同じ（save.jsonの位置にスナップショットを置く）。常時記録は開始しない
            // Same load path as a normal boot (the snapshot sits where save.json would); capture stays off
            var tempRoot = Path.Combine(Path.GetTempPath(), $"moorestech-replay-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            var savePath = Path.Combine(tempRoot, "save.json");
            File.Copy(request.SnapshotFilePath, savePath);

            var options = new MoorestechServerDIContainerOptions(request.ServerDataDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(request.ServerDataDirectory, savePath),
            };
            var (packetResponseCreator, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loadedTick = GameUpdater.CurrentTick;

            var records = ReceivedPacketLogReader.ReadAll(request.PacketLogFilePaths);
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            var context = new PacketResponseContext(null);
            var replayed = 0;
            var next = 0;

            // 記録tick == 次のtick のパケットを積んでから Update する。tick末尾でまとめて処理される
            // Enqueue packets whose recorded tick equals the next tick, then Update; they are processed together at tick end
            while (GameUpdater.CurrentTick < request.TargetTick)
            {
                var nextTick = GameUpdater.CurrentTick + 1;
                while (next < records.Count && records[next].Tick < nextTick) next++;
                while (next < records.Count && records[next].Tick == nextTick)
                {
                    queue.Enqueue(new ReplayPacketEntry(packetResponseCreator, context, records[next].Payload, null));
                    replayed++;
                    next++;
                }
                GameUpdater.Update();
            }

            var json = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            Debug.Log($"再生完了 loaded:{loadedTick} reached:{GameUpdater.CurrentTick} packets:{replayed}");
            Directory.Delete(tempRoot, true);
            return new ReplayResult(loadedTick, GameUpdater.CurrentTick, replayed, json);
        }
    }
}
