using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V4→V5移行: 旧ベルコン(VanillaBeltConveyorComponent形式)のstateを落とす。載っていたアイテムは消滅する
    // V4→V5 migration: drop the old belt conveyor (VanillaBeltConveyorComponent shape) state; the items carried on it vanish
    // 新形式(BeltConveyorSaveStateComponent)はキーが無ければ空として載るので、新キーは書かない
    // The new shape (BeltConveyorSaveStateComponent) loads as empty when its key is absent, so no new key is written
    public sealed class SaveMigrationStepV4ToV5 : ISaveMigrationStep
    {
        // 旧コンポーネントのSaveKey(typeof FullName)。ステップは前の版の型を参照しないので文字列で持つ
        // The old component's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        public const string OldBeltSaveKey = "Game.Block.Blocks.BeltConveyor.VanillaBeltConveyorComponent";

        public int FromVersion => 4;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!(save["world"] is JArray world)) return Fail($"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}");

            var droppedBlocks = 0;
            var droppedItems = 0;
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Fail($"world要素がオブジェクトではないため変換できません。 type={blockToken.Type}");

                // stateが無いブロックは旧ベルコンではないので対象外
                // A block without state is not an old belt, so it is out of scope
                var stateToken = block["state"];
                if (stateToken == null || stateToken.Type == JTokenType.Null) continue;
                if (!(stateToken is JObject state)) return Fail($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}");

                // 旧キーが無ければ既に新形式か他のブロック(冪等)
                // Without the old key the block is already in the new shape or another kind (idempotent)
                var oldToken = state[OldBeltSaveKey];
                if (oldToken == null) continue;

                // 旧形式はアイテムのJSON文字列かnullの配列。他の形は旧ベルコンとして読めないので拒否する
                // The old shape is an array of item JSON strings or nulls; any other shape is unreadable as an old belt and is refused
                if (oldToken.Type != JTokenType.Null && !(oldToken is JArray)) return Fail($"{OldBeltSaveKey} が配列ではありません。 type={oldToken.Type}");
                if (oldToken is JArray items)
                {
                    foreach (var item in items)
                    {
                        if (item.Type != JTokenType.Null && item.Type != JTokenType.String) return Fail($"{OldBeltSaveKey} の要素が文字列でもnullでもありません。 type={item.Type}");
                        if (item.Type == JTokenType.String) droppedItems++;
                    }
                }

                state.Remove(OldBeltSaveKey);
                droppedBlocks++;
            }

            Debug.Log($"セーブを版4から版5へ変換しました。旧ベルコンのstate除去={droppedBlocks}件 消滅したアイテム={droppedItems}個");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版4から版5へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
