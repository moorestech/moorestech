using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行中セッションを current/ に追記し、終了時に outbox の1箱へ畳む。追記形式なので落ちても直前まで残る
    // Appends the in-flight session under current/ and folds it into one outbox box at the end; append-only survives a crash
    public static class ProgressRecordFiles
    {
        public static void WriteHeader(ProgressRecordHeader header)
        {
            Directory.CreateDirectory(GameSystemPaths.ProgressRecordCurrentDirectory);
            File.WriteAllText(ProgressRecordPaths.CurrentHeaderPath, header.ToJson());
        }

        public static void AppendEvent(ProgressEventEntry entry)
        {
            Directory.CreateDirectory(GameSystemPaths.ProgressRecordCurrentDirectory);
            File.AppendAllText(ProgressRecordPaths.CurrentEventsPath, entry.ToJsonLine() + "\n");
        }

        public static bool HasCurrentSession()
        {
            return File.Exists(ProgressRecordPaths.CurrentHeaderPath);
        }

        public static ProgressRecordHeader ReadHeader()
        {
            if (!HasCurrentSession()) return null;
            return ProgressRecordHeader.FromJson(File.ReadAllText(ProgressRecordPaths.CurrentHeaderPath));
        }

        public static List<ProgressEventEntry> ReadEvents()
        {
            var entries = new List<ProgressEventEntry>();
            if (!File.Exists(ProgressRecordPaths.CurrentEventsPath)) return entries;
            foreach (var line in File.ReadAllLines(ProgressRecordPaths.CurrentEventsPath))
            {
                if (line.Length == 0) continue;
                var entry = ProgressEventEntry.FromJsonLine(line);
                if (entry != null) entries.Add(entry);
            }
            return entries;
        }

        // 閉じられなければ null。呼び出し側は理由をログへ出す（無音で消さない）
        // Returns null when it cannot close; the caller logs the reason and never drops it silently
        public static string CloseCurrentInto(string endReason, DateTime sessionEndUtc)
        {
            var header = ReadHeader();
            if (header == null)
            {
                if (HasCurrentSession()) Debug.LogWarning("進行記録のヘッダが壊れているためこのセッションは送れません");
                ClearCurrent();
                return null;
            }

            var directory = ProgressRecordPaths.CreateOutboxDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));
            File.WriteAllText(Path.Combine(directory, ProgressRecordPaths.RecordFileName), ProgressRecordComposer.Compose(header, ReadEvents(), endReason, sessionEndUtc));
            File.WriteAllText(Path.Combine(directory, ProgressRecordPaths.ReadyMarkerFileName), "");
            ClearCurrent();
            Debug.Log($"進行記録を書きました {directory} endReason:{endReason}");
            return directory;
        }

        public static void ClearCurrent()
        {
            if (File.Exists(ProgressRecordPaths.CurrentHeaderPath)) File.Delete(ProgressRecordPaths.CurrentHeaderPath);
            if (File.Exists(ProgressRecordPaths.CurrentEventsPath)) File.Delete(ProgressRecordPaths.CurrentEventsPath);
        }
    }
}
