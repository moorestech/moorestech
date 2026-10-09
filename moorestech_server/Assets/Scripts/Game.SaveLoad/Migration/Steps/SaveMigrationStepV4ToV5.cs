using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V4→V5移行（ADR0077）: BPのオフセットを外接箱最小角基準へ平行移動し、配線リストを空で足す
    // V4→V5 migration (ADR 0077): shift blueprint offsets to the extent's min corner and add empty line lists
    // 最小角は各ブロック原点(=MinPos)の成分最小なのでマスタを引かない
    // The min corner is the component-wise min of block origins (= MinPos), so the master is never read
    public sealed class SaveMigrationStepV4ToV5 : ISaveMigrationStep
    {
        private static readonly string[] OffsetKeys = { "offsetX", "offsetY", "offsetZ" };

        public int FromVersion => 4;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // BPを1件も持たないセーブは節自体が無いことがある
            // A save without blueprints may lack the section entirely
            var blueprintsToken = save["blueprints"];
            if (blueprintsToken == null || blueprintsToken.Type == JTokenType.Null) return SaveMigrationStepResult.Converted(save);
            if (!(blueprintsToken is JArray blueprints)) return Fail($"blueprintsが配列ではありません。 type={blueprintsToken.Type}");

            // 変換前に全BPの形を検証し、途中まで書き換えた状態を残さない
            // Validate every blueprint before rewriting, so no half-converted state is left behind
            foreach (var blueprintToken in blueprints)
            {
                var invalidReason = FindInvalidReason(blueprintToken);
                if (invalidReason != null) return Fail(invalidReason);
            }

            foreach (var blueprintToken in blueprints)
            {
                var blueprint = (JObject)blueprintToken;
                ShiftToMinCorner((JArray)blueprint["blocks"]);
                blueprint["wires"] = new JArray();
                blueprint["chains"] = new JArray();
            }

            Debug.Log($"セーブを版4から版5へ変換しました。BP={blueprints.Count}件");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            string FindInvalidReason(JToken blueprintToken)
            {
                if (!(blueprintToken is JObject blueprint)) return $"blueprints要素がオブジェクトではありません。 type={blueprintToken.Type}";
                if (!(blueprint["blocks"] is JArray blocks)) return $"BPのblocksが配列ではありません。 guid={blueprint["guid"]}";
                foreach (var blockToken in blocks)
                {
                    if (!(blockToken is JObject block)) return $"BPのblocks要素がオブジェクトではありません。 guid={blueprint["guid"]} type={blockToken.Type}";
                    foreach (var key in OffsetKeys)
                    {
                        var offsetToken = block[key];
                        if (offsetToken?.Type != JTokenType.Integer) return $"BPのブロックに整数の{key}がありません。 guid={blueprint["guid"]}";
                        // 現在カルチャの負号を使わず、整数値を不変カルチャで検証する
                        // Validate the integer value with invariant formatting, independent of the current culture's minus sign
                        var offsetText = Convert.ToString(((JValue)offsetToken).Value, CultureInfo.InvariantCulture);
                        if (!int.TryParse(offsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        {
                            return $"BPの{key}が整数範囲外です。 guid={blueprint["guid"]}";
                        }
                    }
                }

                // 平行移動後も保存型の整数範囲に収まることを先に確認する
                // Check the normalized span fits the saved integer type before mutation
                foreach (var key in OffsetKeys)
                {
                    var min = int.MaxValue;
                    var max = int.MinValue;
                    foreach (var block in blocks)
                    {
                        min = Mathf.Min(min, block[key].Value<int>());
                        max = Mathf.Max(max, block[key].Value<int>());
                    }

                    if ((long)max - min > int.MaxValue) return $"BPの{key}の幅が整数範囲外です。";
                }

                return null;
            }

            void ShiftToMinCorner(JArray blocks)
            {
                if (blocks.Count == 0) return;
                foreach (var key in OffsetKeys)
                {
                    var min = int.MaxValue;
                    foreach (var block in blocks) min = Mathf.Min(min, block[key].Value<int>());
                    foreach (var block in blocks) block[key] = block[key].Value<int>() - min;
                }
            }

            SaveMigrationStepResult Fail(string failureReason)
            {
                Debug.LogWarning($"セーブを版4から版5へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
