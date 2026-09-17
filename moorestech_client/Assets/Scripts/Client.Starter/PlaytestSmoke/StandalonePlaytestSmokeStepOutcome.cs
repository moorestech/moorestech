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

        public static StandalonePlaytestSmokeStepOutcome Succeeded(string value)
        {
            return new StandalonePlaytestSmokeStepOutcome(true, value, "");
        }

        public static StandalonePlaytestSmokeStepOutcome Failed(string failureReason)
        {
            return new StandalonePlaytestSmokeStepOutcome(false, "", failureReason);
        }
    }
}
