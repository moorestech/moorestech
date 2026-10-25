using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// 通常機械とクリーンルーム機械の加工状態に継承レベル null を明示で足す（冪等）。版7に継承レシピは無いため「継承しない」が正しい
    /// Add an explicit null inherited level to normal and clean-room machine processing state (idempotent); version 7 had no inheritance recipes, so "no inheritance" is exact
    /// </summary>
    public sealed class MachineInheritedLevelFiller : IV7ToV8MigrationPart
    {
        // コンポーネントのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // Component SaveKeys (typeof FullName); the step never references live types, so they are kept as strings
        private const string VanillaMachineSaveKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        private const string CleanRoomMachineSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string InheritedLevelKey = "inheritedLevel";

        private readonly List<JObject> _vanillaProcessors = new();
        private readonly List<JObject> _cleanRoomProcessors = new();
        private int _nullVanillaProcessorCount;

        public bool TryPrepare(JObject save, out string failureReason)
        {
            // world→ブロック→state の走査と拒否文は全部品で共有する
            // The world->block->state walk and its refusal texts are shared by every part
            if (!V7ToV8SaveJson.TryCollectBlockStates(save, out var states, out failureReason)) return false;

            foreach (var state in states)
            {
                // 通常機械はprocessorサブオブジェクトへ足す
                // A normal machine gets the key inside its processor sub-object
                var vanillaToken = state[VanillaMachineSaveKey];
                if (!V7ToV8SaveJson.IsAbsentOrNull(vanillaToken))
                {
                    if (!(vanillaToken is JObject vanilla)) return V7ToV8SaveJson.Reject($"{VanillaMachineSaveKey} がオブジェクトではありません。 type={vanillaToken.Type}", out failureReason);
                    var processorToken = vanilla["processor"];
                    // processorの明示nullはロード側も警告して復元を見送るので、止めずに台数だけ残す（清浄室機械の部品と同じ扱い）
                    // An explicit null processor is skipped with a warning by the loader too, so count it instead of refusing
                    if (V7ToV8SaveJson.IsExplicitNull(processorToken)) _nullVanillaProcessorCount++;
                    else if (!(processorToken is JObject processor)) return V7ToV8SaveJson.Reject($"{VanillaMachineSaveKey} の processor がオブジェクトではありません。 type={processorToken?.Type}", out failureReason);
                    else if (!processor.ContainsKey(InheritedLevelKey)) _vanillaProcessors.Add(processor);
                }

                // クリーンルーム機械は直下へ足す
                // A clean-room machine gets the key at its top level
                var cleanRoomToken = state[CleanRoomMachineSaveKey];
                if (V7ToV8SaveJson.IsAbsentOrNull(cleanRoomToken)) continue;
                if (!(cleanRoomToken is JObject cleanRoom)) return V7ToV8SaveJson.Reject($"{CleanRoomMachineSaveKey} がオブジェクトではありません。 type={cleanRoomToken.Type}", out failureReason);
                if (!cleanRoom.ContainsKey(InheritedLevelKey)) _cleanRoomProcessors.Add(cleanRoom);
            }

            return true;
        }

        public string ApplyAndSummarize()
        {
            foreach (var processor in _vanillaProcessors) processor[InheritedLevelKey] = JValue.CreateNull();
            foreach (var cleanRoom in _cleanRoomProcessors) cleanRoom[InheritedLevelKey] = JValue.CreateNull();
            return $"継承レベルを足した通常機械={_vanillaProcessors.Count}台 クリーンルーム機械={_cleanRoomProcessors.Count}台 processorが null で飛ばした通常機械={_nullVanillaProcessorCount}台";
        }
    }
}
