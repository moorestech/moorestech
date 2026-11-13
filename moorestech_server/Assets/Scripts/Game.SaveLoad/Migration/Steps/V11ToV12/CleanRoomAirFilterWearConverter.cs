using System.Collections.Generic;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V11ToV12
{
    /// <summary>
    /// 清浄機の state の摩耗量を消し、経過秒0を入れる（冪等）。旧値は吸い取った不純物の量で経過時間へ換算する根拠が無いため、装填中のフィルターを新品扱いにする
    /// Drop the purifier state's wear and set zero elapsed seconds (idempotent); the old value was absorbed impurity with no basis for an elapsed time, so the loaded filter is treated as new
    /// </summary>
    internal sealed class CleanRoomAirFilterWearConverter : ISaveMigrationPart
    {
        // 清浄機コンポーネントのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // The purifier component's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        private const string AirFilterSaveKey = "Game.Block.Blocks.CleanRoom.CleanRoomAirFilterComponent";
        private const string WearAccumulationKey = "wearAccumulation";
        private const string FilterWearSecondsKey = "filterWearSeconds";

        private readonly List<JObject> _targets = new();
        private int _nullAirFilterCount;

        public bool TryPrepare(JObject save, out string failureReason)
        {
            // world→ブロック→state の走査と拒否文は全部品で共有する
            // The world->block->state walk and its refusal texts are shared by every part
            if (!MigrationSaveJson.TryCollectBlockStates(save, out var states, out failureReason)) return false;

            foreach (var state in states)
            {
                // 清浄機以外はキーが無いので対象外
                // Blocks other than purifiers lack the key and are out of scope
                var airFilterToken = state[AirFilterSaveKey];
                if (airFilterToken == null) continue;

                // 明示nullはロード側も読み飛ばす形なので、止めずに台数だけ残す
                // An explicit null is also skipped by the loader, so count it instead of refusing
                if (MigrationSaveJson.IsExplicitNull(airFilterToken))
                {
                    _nullAirFilterCount++;
                    continue;
                }

                if (!(airFilterToken is JObject airFilterState)) return MigrationSaveJson.Reject($"{AirFilterSaveKey} がオブジェクトではありません。 type={airFilterToken.Type}", out failureReason);
                _targets.Add(airFilterState);
            }

            return true;
        }

        public string ApplyAndSummarize()
        {
            // 経過秒が既にある清浄機（変換済み）は触らず、旧摩耗量だけを消して新品扱いにする
            // Leave purifiers that already have elapsed seconds (already converted) untouched; drop only the old wear and treat the filter as new
            var convertedCount = 0;
            foreach (var airFilterState in _targets)
            {
                airFilterState.Remove(WearAccumulationKey);
                if (airFilterState.ContainsKey(FilterWearSecondsKey)) continue;
                airFilterState[FilterWearSecondsKey] = 0d;
                convertedCount++;
            }

            return $"摩耗量を経過秒0へ置き換えた清浄機={convertedCount}台 state が null で飛ばした清浄機={_nullAirFilterCount}台";
        }
    }
}
