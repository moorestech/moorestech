using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration
{
    /// <summary>1手の変換結果。変換できなかったことを理由つきで表し、失敗を成功へ畳ませない</summary>
    /// <summary>One hop's outcome; it can say "not converted" with a reason so a failure never folds into success</summary>
    public sealed class SaveMigrationStepResult
    {
        public bool IsConverted { get; }
        public string FailureReason { get; }
        public JObject Save { get; }

        private SaveMigrationStepResult(bool isConverted, string failureReason, JObject save)
        {
            IsConverted = isConverted;
            FailureReason = failureReason;
            Save = save;
        }

        public static SaveMigrationStepResult Converted(JObject save)
        {
            return new SaveMigrationStepResult(true, null, save);
        }

        // 失敗のセーブは返さない。半端に変換された木を連鎖へ渡すと、版だけ進んで壊れた形が残る
        // A failure carries no save; handing a half-converted tree to the chain would stamp the version onto a broken shape
        public static SaveMigrationStepResult Failed(string reason)
        {
            return new SaveMigrationStepResult(false, reason, null);
        }
    }
}
