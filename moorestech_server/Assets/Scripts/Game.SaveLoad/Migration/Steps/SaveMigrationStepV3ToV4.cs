using System;
using Game.SaveLoad.Migration.Steps.V3ToV4;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V3→V4移行（ADR0076）: 電線・歯車チェーンの各接続へ、線種別ごとの唯一の種類を書き込む
    // V3→V4 migration (ADR 0076): write each kind's single connect tool into every wire and gear-chain connection
    // 素材は見ずマスタも引かない（裁定 .decisions/2026-10-04-旧セーブの線は線種別ごとの唯一の種類を割り当てて移行する.md）
    // Materials are ignored and the master is never read (ruling .decisions/2026-10-04-旧セーブの線は線種別ごとの唯一の種類を割り当てて移行する.md)
    public sealed class SaveMigrationStepV3ToV4 : ISaveMigrationStep
    {
        public static readonly Guid ElectricWireConnectToolGuid = Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2");
        public static readonly Guid GearChainConnectToolGuid = Guid.Parse("6c1dab62-be1e-45bc-abe7-69ec7d109b88");

        // コンポーネントのSaveKey（nameof）。ステップは前の版の型を参照しないので文字列で持つ
        // The components' SaveKeys (nameof); the step never references live types, so they are kept as strings
        private const string WireSaveKey = "ElectricWireConnectorComponent";
        private const string ChainSaveKey = "GearChainPoleComponent";

        public int FromVersion => 3;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!(save["world"] is JArray world)) return Fail($"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}");

            var wireFilled = 0;
            var chainFilled = 0;
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Fail($"world要素がオブジェクトではないため変換できません。 type={blockToken.Type}");

                // stateが無いブロックは接続を持たないので対象外
                // A block without state holds no connections, so it is out of scope
                var stateToken = block["state"];
                if (stateToken == null || stateToken.Type == JTokenType.Null) continue;
                if (!(stateToken is JObject state)) return Fail($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}");

                // 電線とチェーンへ固定の種類を書き込む
                // Write each kind's fixed tool into wire and chain connections
                var wire = ConnectionToolGuidFiller.Fill(state, WireSaveKey, ElectricWireConnectToolGuid);
                if (!wire.IsFilled) return Fail(wire.FailureReason);
                var chain = ConnectionToolGuidFiller.Fill(state, ChainSaveKey, GearChainConnectToolGuid);
                if (!chain.IsFilled) return Fail(chain.FailureReason);
                wireFilled += wire.FilledCount;
                chainFilled += chain.FilledCount;
            }

            Debug.Log($"セーブを版3から版4へ変換しました。電線の種類補填={wireFilled}件 チェーンの種類補填={chainFilled}件");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版3から版4へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
