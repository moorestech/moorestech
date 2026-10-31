using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// 宇宙エレベーターのケーブルプールの節を0で足す（冪等）。版7までケーブルを軌道へ運ぶ積荷表の行は無く、0が実値である
    /// Add the space-elevator cable pool section at zero (idempotent); up to version 7 no payload row could carry cables to orbit, so zero is the real value
    /// </summary>
    public sealed class SpaceElevatorCablePoolFiller : IV7ToV8MigrationPart
    {
        private const string PoolKey = "spaceElevatorCablePool";
        private const string CountKey = "cableCount";

        private JObject _fillTarget;

        public bool TryPrepare(JObject save, out string failureReason)
        {
            failureReason = null;
            var existing = save[PoolKey];

            // 欠損と明示nullは0のプールで補う
            // Fill both absence and an explicit null with an empty pool
            if (V7ToV8SaveJson.IsAbsentOrNull(existing))
            {
                _fillTarget = save;
                return true;
            }

            // 整数のcableCountを持つ節は形だけ確かめて素通しする。値の範囲はロード時のRestoreだけが見る
            // A section with an integer cableCount passes after a shape check; only Restore at load checks the value range
            if (existing is JObject pool && pool[CountKey]?.Type == JTokenType.Integer) return true;
            failureReason = $"{PoolKey} が整数の {CountKey} を持つオブジェクトではありません。 value={existing}";
            return false;
        }

        public string ApplyAndSummarize()
        {
            if (_fillTarget == null) return "ケーブルプール=既存の節を保持";
            _fillTarget[PoolKey] = new JObject { [CountKey] = 0 };
            return "ケーブルプール=0で追加";
        }
    }
}
