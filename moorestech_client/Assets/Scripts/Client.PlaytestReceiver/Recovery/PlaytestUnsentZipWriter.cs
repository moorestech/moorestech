using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    public sealed class PlaytestUnsentZipResult
    {
        public readonly int BoxCount;
        public readonly IReadOnlyList<PlaytestExportMissing> Missing;
        public PlaytestUnsentZipResult(int boxCount, IReadOnlyList<PlaytestExportMissing> missing) { BoxCount = boxCount; Missing = missing; }
    }

    // 未送信の箱を提出用のzip 1本にまとめる。読めない箱・ファイルは飛ばして一覧に残す
    // Packs unsent boxes into one zip for hand-in; unreadable boxes and files are skipped and listed
    public static class PlaytestUnsentZipWriter
    {
        public static PlaytestUnsentZipResult Write(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope, string zipPath, DateTime exportedAtUtc)
        {
            var boxes = new JArray();
            var missing = new List<PlaytestExportMissing>();
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                AddOutbox(archive, "reports", directories.ReportOutbox);
                if (scope.IncludesProgress()) AddOutbox(archive, "progress", directories.ProgressOutbox);
                WriteExportJson(archive);
            }
            return new PlaytestUnsentZipResult(boxes.Count, missing);

            #region Internal

            void AddOutbox(ZipArchive archive, string kind, string outbox)
            {
                IReadOnlyList<string> boxDirectories;
                // 外部ディスク境界。outboxごと読めなければその種別を欠落にして続ける
                // External disk boundary: an unreadable outbox is listed as missing and the export continues
                try { boxDirectories = PlaytestOutboxScanner.EnumerateBoxDirectories(outbox); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Skip(kind, error);
                    return;
                }
                foreach (var box in boxDirectories) AddBox(archive, kind, box);
            }

            void AddBox(ZipArchive archive, string kind, string box)
            {
                var id = Path.GetFileName(box);
                PlaytestOutboxBoxKind state;
                string[] files;
                // 外部ディスク境界。箱を分類・列挙できなければ箱ごと欠落にする
                // External disk boundary: a box that cannot be classified or listed is missing as a whole
                try
                {
                    state = PlaytestOutboxBoxState.Classify(box);
                    if (state != PlaytestOutboxBoxKind.Pending && state != PlaytestOutboxBoxKind.Failed) return; // silent-ok: 送信済み・未完成の箱は対象外 / 件数表示にも出ない
                    files = Directory.GetFiles(box, "*", SearchOption.AllDirectories);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Skip($"{kind}/{id}", error);
                    return;
                }
                boxes.Add(new JObject { ["kind"] = kind, ["id"] = id, ["state"] = state == PlaytestOutboxBoxKind.Failed ? "failed" : "pending" });
                foreach (var file in files)
                {
                    var relative = PlaytestOutboxScanner.ToRelativePath(box, file);
                    if (relative == PlaytestOutboxScanner.LockMarker) continue; // silent-ok: 排他用の空ファイル / 提出物に意味がない
                    AddFile(archive, $"{kind}/{id}/{relative}", file);
                }
            }

            void AddFile(ZipArchive archive, string entryName, string file)
            {
                // 外部ディスク境界。他プロセスのロックや消失で読めないファイルだけを欠落にする
                // External disk boundary: only files locked by another process or gone are listed as missing
                FileStream source;
                try { source = File.OpenRead(file); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Skip(entryName, error);
                    return;
                }
                // zipへの書き込み失敗は欠落にせず、書き出し自体の失敗としてPackUnsentへ届かせる
                // A zip write failure is not a missing item; it propagates to PackUnsent as an export failure
                using (source)
                {
                    using var target = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Fastest).Open();
                    source.CopyTo(target);
                }
            }

            void Skip(string item, Exception error)
            {
                Debug.LogWarning($"[PlaytestReceiver] export skipped {item}: {error.Message}");
                missing.Add(new PlaytestExportMissing(item, $"{error.GetType().Name}: {error.Message}"));
            }

            void WriteExportJson(ZipArchive archive)
            {
                var missingJson = new JArray();
                foreach (var entry in missing) missingJson.Add(new JObject { ["item"] = entry.Item, ["reason"] = entry.Reason });
                var meta = new JObject
                {
                    ["schemaVersion"] = 1,
                    ["exportedAt"] = exportedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["boxes"] = boxes,
                    ["missing"] = missingJson,
                };
                using var writer = new StreamWriter(archive.CreateEntry("export.json").Open());
                writer.Write(meta.ToString());
            }

            #endregion
        }
    }
}
