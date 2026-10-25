using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// ロケットの初回打ち上げ履歴を未打ち上げで足す（冪等）。版7まで発射台は存在しないので未打ち上げが唯一の正しい値
    /// Add the rocket first-launch history as not yet launched (idempotent); no launch pad existed up to version 7, so not-launched is the only correct value
    /// </summary>
    public sealed class RocketLaunchHistoryFiller : IV7ToV8MigrationPart
    {
        private const string HistoryKey = "rocketHasLaunchedOnce";

        private JObject _fillTarget;

        public bool TryPrepare(JObject save, out string failureReason)
        {
            failureReason = null;
            var history = save[HistoryKey];

            // 欠損と明示nullは未打ち上げで補う
            // Fill both absence and an explicit null with not-launched
            if (V7ToV8SaveJson.IsAbsentOrNull(history))
            {
                _fillTarget = save;
                return true;
            }

            // 真偽値は素通しする（再実行や、既に履歴を持つ形）
            // Pass a boolean through (a rerun, or a save that already has the history)
            if (history.Type == JTokenType.Boolean) return true;
            failureReason = $"{HistoryKey} が真偽値ではありません。 type={history.Type} value={history}";
            return false;
        }

        public string ApplyAndSummarize()
        {
            if (_fillTarget == null) return "初回打ち上げ履歴=既存の値を保持";
            _fillTarget[HistoryKey] = false;
            return "初回打ち上げ履歴=未打ち上げで追加";
        }
    }
}
