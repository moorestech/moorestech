using System;

namespace Client.Game.InGame.Playtest.Progress.Record
{
    // 終了理由を record.json の契約値へ綴る唯一の場所。C#の内部は enum のまま持ち回す（shared-contracts §3）
    // The only place spelling the end reason as record.json's contract value; C# carries the enum everywhere else (shared-contracts §3)
    internal static class ProgressEndReasonJson
    {
        public static string ToContractText(ProgressEndReason endReason)
        {
            return endReason switch
            {
                ProgressEndReason.Quit => "quit",
                ProgressEndReason.CrashRecovered => "crash-recovered",
                ProgressEndReason.InitializationFailed => "init-failed",
                _ => throw new ArgumentOutOfRangeException(nameof(endReason), endReason, "契約値の無い終了理由"),
            };
        }
    }
}
