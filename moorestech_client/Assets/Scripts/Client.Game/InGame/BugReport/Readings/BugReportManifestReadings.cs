using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.GameUiExceptions;
using Client.Game.InGame.BugReport.WebUiHealth;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Readings
{
    /// <summary>
    /// 確保データから manifest の読み取り欄（webUiHealth・gameUiExceptions）を詰める。数えられなかった UI の例外は MarkGameUiExceptionsUnknown の 1 本で不明と欠損を組で書く
    /// Fills the manifest's reading fields (webUiHealth, gameUiExceptions) from captured data; uncountable UI exceptions go through MarkGameUiExceptionsUnknown alone, writing unknown and the gap together
    /// </summary>
    public static class BugReportManifestReadings
    {
        public static void Apply(BugReportManifest manifest, BugReportCapturedData data)
        {
            ApplyWebUiHealth();
            ApplyGameUiExceptions();

            #region Internal

            void ApplyWebUiHealth()
            {
                // 確保が箱を埋め損ねたら unknown を入れ、常時非 null の契約を守りつつ欠損と開発者ログへ残す
                // If the capture left the box empty, unknown is recorded so the always-non-null contract holds, with the gap in the manifest and the developer log
                if (data.WebUiHealth == null)
                {
                    Debug.LogError("[BugReport] 確保データにWebホスト・CEFの値が無い。webUiHealth は unknown で出す");
                    manifest.WebUiHealth = WebUiHealthReading.UnknownForPreviousSession();
                    manifest.AddMissing("webUiHealth", "確保データにWebホスト・CEFの値が無い");
                    return;
                }
                manifest.WebUiHealth = data.WebUiHealth;
            }

            void ApplyGameUiExceptions()
            {
                // 確保時に数え手の値を取れなかった箱（記録を集めない起動など）は数えていない。0 件と読ませない
                // A box whose capture got no tally value (such as a boot that collects no records) was not counted; never let it read as zero
                if (data.GameUiExceptions.Status == GameUiExceptionStatus.Unknown)
                {
                    MarkGameUiExceptionsUnknown(manifest, "確保時に UI の例外の数を取れなかった（記録を集めない起動）");
                    return;
                }
                manifest.GameUiExceptions = data.GameUiExceptions;
            }

            #endregion
        }

        // 数えていない箱の唯一の書き方。0 件と読ませないよう、不明の値と欠損の行を必ず組で書く（欠損は AddMissing が開発者ログにも出す）
        // The only way to write an uncounted box; unknown and the gap entry always go together so it never reads as zero (AddMissing also logs it for developers)
        public static void MarkGameUiExceptionsUnknown(BugReportManifest manifest, string reason)
        {
            manifest.GameUiExceptions = new GameUiExceptionsUnknown();
            manifest.AddMissing("gameUiExceptions", reason);
        }
    }
}
