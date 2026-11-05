namespace Client.Input
{
    /// <summary>
    /// 入力の通過可否（抑止・ESCラッチ）の判定と抑止ログを 1 か所に集め、InputKey と HybridInput が同じ順序で使う
    /// Gathers the pass/block decision (suppression and ESC latch) and its logging so InputKey and HybridInput apply the same order
    /// </summary>
    internal static class InputSuppressionFilter
    {
        // 押下系。ESCラッチ → 抑止の順で、抑止したときだけログを残す
        // Press-type reads: ESC latch first, then suppression, logging only when suppressed
        public static bool FilterKeyPress(bool pressed, InputSuppressionScope? scope, bool isEscapeKey)
        {
            if (!pressed) return false;
            if (isEscapeKey && UiInputExclusivity.IsEscapeConsumedThisFrame) return false;
            return PassSuppression(scope);
        }

        // 解放系。ESCラッチは通さない（解放は今までどおり届ける）
        // Release-type reads: the ESC latch does not apply (releases are still delivered)
        public static bool FilterKeyRelease(bool released, InputSuppressionScope? scope)
        {
            return released && PassSuppression(scope);
        }

        // 値入力。既定値以外が抑止されたときだけログを残す
        // Value reads: log only when a non-default value is suppressed
        public static bool FilterValueInput(bool hasNonDefaultValue, InputSuppressionScope? scope)
        {
            if (!scope.HasValue || !UiInputExclusivity.IsSuppressed(scope.Value)) return true;
            if (hasNonDefaultValue) UiInputExclusivity.ProbeSuppressed(scope.Value);
            return false;
        }

        private static bool PassSuppression(InputSuppressionScope? scope)
        {
            if (!scope.HasValue || !UiInputExclusivity.IsSuppressed(scope.Value)) return true;
            UiInputExclusivity.ProbeSuppressed(scope.Value);
            return false;
        }
    }
}
