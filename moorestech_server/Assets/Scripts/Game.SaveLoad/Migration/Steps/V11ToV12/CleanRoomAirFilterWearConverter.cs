using System.Collections.Generic;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V11ToV12
{
    /// <summary>
    /// 清浄機の摩耗量を消し新品扱いにし、在室なしを書く（冪等）
    /// Drop purifier wear, treat the loaded filter as new, and write not-serving (idempotent)
    /// </summary>
    internal sealed class CleanRoomAirFilterWearConverter : ISaveMigrationPart
    {
        // 清浄機コンポーネントのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // The purifier component's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        private const string AirFilterSaveKey = "Game.Block.Blocks.CleanRoom.CleanRoomAirFilterComponent";
        // 旧摩耗量は時間へ換算できず新品扱い
        // The old wear cannot be converted to time, so the filter is treated as new
        private const string WearAccumulationKey = "wearAccumulation";
        private const string FilterWearSecondsKey = "filterWearSeconds";
        // 旧版は在室を持たないので在室なしで始め、ロード後最初の判定段で部屋の中の清浄機へ配られる
        // Old saves lack serving state, so start not serving; the first pass after load pushes it to purifiers inside rooms
        private const string IsServingRoomKey = "isServingRoom";

        private readonly List<JObject> _targets = new();
        private int _nullAirFilterCount;

        public bool TryPrepare(JObject save, out string failureReason)
        {
            // 走査と拒否文を全部品で共有
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
            // 変換済みのキーは触らず、旧摩耗量を消して欠けたキーだけ足す
            // Keep already-converted keys; drop the old wear and add only the missing keys
            var convertedCount = 0;
            foreach (var airFilterState in _targets)
            {
                airFilterState.Remove(WearAccumulationKey);
                var hasWearSeconds = airFilterState.ContainsKey(FilterWearSecondsKey);
                var hasServingRoom = airFilterState.ContainsKey(IsServingRoomKey);
                if (hasWearSeconds && hasServingRoom) continue;
                if (!hasWearSeconds) airFilterState[FilterWearSecondsKey] = 0d;
                if (!hasServingRoom) airFilterState[IsServingRoomKey] = false;
                convertedCount++;
            }

            return $"摩耗量を経過秒0・在室なしへ置き換えた清浄機={convertedCount}台 state が null で飛ばした清浄機={_nullAirFilterCount}台";
        }
    }
}
