using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V6→V7移行: パイプの中身と面速度を double(流体量)から 1/1000 単位の整数へ変える(ADR 0090)。`amount`→`amountMilli`、`faceVelocities[].velocity`→`velocityMilli`、どちらも最近傍に丸める
    // V6→V7 migration: convert pipe contents and face velocities from doubles (amount) to 1/1000-unit integers (ADR 0090). `amount`→`amountMilli`, `faceVelocities[].velocity`→`velocityMilli`, both rounded to nearest
    // 機械のタンク(FluidContainerSaveJsonObject)は double のままなので触らない
    // Machine tanks (FluidContainerSaveJsonObject) stay doubles and are left untouched
    public sealed class SaveMigrationStepV6ToV7 : ISaveMigrationStep
    {
        // パイプの SaveKey(typeof FullName)。ステップは前の版の型を参照しないので文字列で持つ
        // The pipe's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        private const string PipeSaveKey = "Game.Block.Blocks.Fluid.FluidPipeSaveComponent";
        private const long UnitsPerAmount = 1000;

        public int FromVersion => 6;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!(save["world"] is JArray world)) return Fail($"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}");

            var convertedPipes = 0;
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Fail($"world要素がオブジェクトではないため変換できません。 type={blockToken.Type}");

                // stateが無いブロックはパイプではないので対象外
                // A block without state is not a pipe, so it is out of scope
                var stateToken = block["state"];
                if (stateToken == null || stateToken.Type == JTokenType.Null) continue;
                if (!(stateToken is JObject state)) return Fail($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}");

                if (!(state[PipeSaveKey] is JObject pipe)) continue;

                // 旧キーが無く新キーがあれば変換済み(冪等)。どちらも無い形は読めないので拒否する
                // Converted already when only the new key exists (idempotent); a shape with neither key is unreadable and refused
                var amountToken = pipe["amount"];
                if (amountToken == null)
                {
                    if (pipe["amountMilli"] != null) continue;
                    return Fail($"{PipeSaveKey} に amount も amountMilli もありません。");
                }
                if (!IsNumber(amountToken)) return Fail($"{PipeSaveKey} の amount が数値ではありません。 type={amountToken.Type}");
                pipe.Remove("amount");
                pipe["amountMilli"] = ToUnits(amountToken.Value<double>());

                var facesToken = pipe["faceVelocities"];
                if (facesToken != null && facesToken.Type != JTokenType.Null)
                {
                    if (!(facesToken is JArray faces)) return Fail($"{PipeSaveKey} の faceVelocities が配列ではありません。 type={facesToken.Type}");
                    foreach (var faceToken in faces)
                    {
                        if (!(faceToken is JObject face)) return Fail($"{PipeSaveKey} の faceVelocities 要素がオブジェクトではありません。 type={faceToken.Type}");
                        var velocityToken = face["velocity"];
                        if (velocityToken == null) return Fail($"{PipeSaveKey} の faceVelocities 要素に velocity がありません。");
                        if (!IsNumber(velocityToken)) return Fail($"{PipeSaveKey} の velocity が数値ではありません。 type={velocityToken.Type}");
                        face.Remove("velocity");
                        face["velocityMilli"] = ToUnits(velocityToken.Value<double>());
                    }
                }
                convertedPipes++;
            }

            Debug.Log($"セーブを版6から版7へ変換しました。パイプの流体量を1/1000単位の整数にした件数={convertedPipes}");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            static bool IsNumber(JToken token)
            {
                return token.Type == JTokenType.Float || token.Type == JTokenType.Integer;
            }

            static long ToUnits(double amount)
            {
                return (long)Math.Round(amount * UnitsPerAmount, MidpointRounding.AwayFromZero);
            }

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版6から版7へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
