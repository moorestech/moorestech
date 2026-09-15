using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 1セッションぶんの current/pid_<PID>/ への書き込み口。書き出した後はヘッダ更新も追記も受け付けず current/ を復活させない
    // The single write port into current/pid_<PID>/ for one session; after the record is written no header update or append may resurrect current/
    internal sealed class ProgressSessionWriter : IDisposable
    {
        private readonly string _sessionDirectory = ProgressCurrentSession.DirectoryForCurrentProcess();
        private ProgressRecordHeader _header;
        private StreamWriter _appender;
        private bool _worldPlayTimeApplied;

        public bool Closed { get; private set; }

        // 書き出し後にヘッダが復活すると、次回起動でイベント0件の偽の記録が1件 outbox に出る
        // A header resurrected after the write would emit one bogus zero-event record from the next boot
        public void WriteHeader(ProgressRecordHeader header)
        {
            if (Closed)
            {
                Debug.LogWarning("進行記録は書き出し済みのためヘッダを更新しません（current/ を復活させない）");
                return;
            }
            _header = header;
            PersistHeader();
        }

        // ワールドのプレイ時間だけはサーバー応答を待つ。2段書きの手順を呼び出し元へ露出させず、この1呼び出しへ畳む
        // Only the world play time awaits the server; the two-step write stays here instead of leaking into the caller
        public void UpdateWorldPlayTime(ProgressWorldPlayTime worldPlayTime)
        {
            if (Closed)
            {
                Debug.LogWarning("ワールドのプレイ時間が終了に間に合わなかったため、worldCreatedAt と totalPlaySeconds は欠損のまま記録されます");
                return;
            }

            _worldPlayTimeApplied = true;

            // 2項目は独立に欠ける。片方の欠損でもう片方の実値を捨てない
            // The two items go missing independently; one gap never discards the other's real value
            if (worldPlayTime.WorldCreatedAtMissingReason != null) _header.AddMissing("worldCreatedAt", worldPlayTime.WorldCreatedAtMissingReason);
            else _header.WorldCreatedAt = worldPlayTime.WorldCreatedAt;

            if (worldPlayTime.TotalPlaySecondsMissingReason != null)
            {
                _header.AddMissing("totalPlaySeconds", worldPlayTime.TotalPlaySecondsMissingReason);
            }
            else
            {
                _header.TotalPlaySecondsAtStart = worldPlayTime.TotalPlaySeconds;
                _header.TotalPlaySecondsCapturedAt = ProgressUtcTime.ToIso(worldPlayTime.CapturedAtUtc);
            }
            PersistHeader();
        }

        // 書き出し済みのセッションへ足すと outbox に出ない行が current/ に湧く。黙らず理由を残して捨てる
        // Appending to a written session would leave lines in current/ that no outbox holds, so it is dropped with a reason
        public void Append(ProgressEventEntry entry)
        {
            if (Closed)
            {
                Debug.LogWarning($"進行記録は書き出し済みのため追記しません type:{entry.Type}");
                return;
            }

            if (_appender == null)
            {
                var opened = ProgressRecordFiles.OpenEventAppender(_sessionDirectory, out _appender);
                if (!opened.Succeeded)
                {
                    Debug.LogError($"進行記録のイベントを追記できません type:{entry.Type}: {opened.FailureReason}");
                    return;
                }
            }

            var appended = ProgressDiskIo.AppendLine(_appender, entry.ToJsonLine());
            if (!appended.Succeeded) Debug.LogError($"進行記録のイベントを追記できません type:{entry.Type}: {appended.FailureReason}");
        }

        // 書けなかったセッションは閉じない（current/ に残し、次回起動の回収へ回す）。中身が無かったのか書けなかったのかは呼び出し側へ分けて返す
        // A session that failed to write stays open in current/ for the next boot; whether it was empty or unwritable is handed back separately
        public ProgressCloseResult Close(string endReason, DateTime sessionEndUtc)
        {
            ReportUnfilledWorldPlayTime();
            CloseAppender();

            var result = ProgressRecordFiles.CloseCurrentInto(_sessionDirectory, endReason, sessionEndUtc, Array.Empty<MissingItem>());
            Closed = result.BundleDirectory != null;
            return result;
        }

        // 追記口だけを閉じる。記録は書き出さないので、このセッションは次回起動の回収対象として current/ に残る
        // Closes only the appender; the record is not written, so this session stays in current/ for the next boot to recover
        public void Dispose()
        {
            CloseAppender();
        }

        private void ReportUnfilledWorldPlayTime()
        {
            if (_worldPlayTimeApplied || _header == null) return;
            const string reason = "サーバー応答が終了までに届かなかった";
            _header.AddMissing("worldCreatedAt", reason);
            _header.AddMissing("totalPlaySeconds", reason);
            PersistHeader();
        }

        private void PersistHeader()
        {
            var written = ProgressRecordFiles.WriteHeader(_sessionDirectory, _header);
            if (!written.Succeeded) Debug.LogError($"進行記録のヘッダを書けませんでした（この記録は欠損したまま閉じられます）: {written.FailureReason}");
        }

        private void CloseAppender()
        {
            if (_appender == null) return;
            var closed = ProgressDiskIo.CloseAppender(_appender);
            if (!closed.Succeeded) Debug.LogError($"進行記録の追記口を閉じられませんでした: {closed.FailureReason}");
            _appender = null;
        }
    }
}
