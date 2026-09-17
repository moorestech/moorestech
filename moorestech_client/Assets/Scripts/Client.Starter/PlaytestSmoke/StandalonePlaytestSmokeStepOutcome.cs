using System;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 1ステップの結末。成功なら後続へ渡す値、失敗なら理由のどちらか一方だけを持つ
    /// One step's outcome; it carries either the value handed onward (success) or the reason (failure), never both
    /// </summary>
    public readonly struct StandalonePlaytestSmokeStepOutcome
    {
        public readonly bool Success;
        public readonly string Value;
        public readonly string FailureReason;

        private StandalonePlaytestSmokeStepOutcome(bool success, string value, string failureReason)
        {
            Success = success;
            Value = value;
            FailureReason = failureReason;
        }

        internal static StandalonePlaytestSmokeStepOutcome Succeeded(string value)
        {
            return new StandalonePlaytestSmokeStepOutcome(true, value, "");
        }

        // 合否はSuccessが持つ。理由の無い失敗は無音の失敗になるため、作る時点で拒否する
        // Success carries pass/fail; a failure without a reason would be silent, so it is refused at construction
        internal static StandalonePlaytestSmokeStepOutcome Failed(string failureReason)
        {
            if (string.IsNullOrEmpty(failureReason)) throw new ArgumentException("a failed smoke step requires a non-empty reason", nameof(failureReason));
            return new StandalonePlaytestSmokeStepOutcome(false, "", failureReason);
        }
    }
}
