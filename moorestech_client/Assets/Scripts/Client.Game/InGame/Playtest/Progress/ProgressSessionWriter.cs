using System;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 1セッションぶんの current/ への書き込み口。書き出した後はヘッダ更新も追記も受け付けず current/ を復活させない
    // The single write port into current/ for one session; after the record is written no header update or append may resurrect current/
    public sealed class ProgressSessionWriter
    {
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
            ProgressRecordFiles.WriteHeader(header);
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
            ProgressRecordFiles.AppendEvent(entry);
        }

        // 書けなければ null。呼び出し側が理由をログへ出す（無音で消さない）
        // Returns null when it could not write; the caller logs the reason and never drops it silently
        public string Close(string endReason, DateTime sessionEndUtc)
        {
            Closed = true;
            return ProgressRecordFiles.CloseCurrentInto(endReason, sessionEndUtc);
        }
    }
}
