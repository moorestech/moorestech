using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Game.SaveLoad.Migration.Steps.Shared;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    // 旧発電機の識別不能な燃焼だけを移行時に停止する
    // Stop only unidentifiable legacy generator burns during migration
    public sealed class ElectricGeneratorFuelStateNormalizer : ISaveMigrationPart
    {
        private const string SaveKey = "Game.Block.Blocks.PowerGenerator.VanillaElectricGeneratorComponent";
        private readonly List<JObject> _targets = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            _targets.Clear();
            if (!MigrationSaveJson.TryCollectBlockStates(save, out var states, out failureReason)) return false;
            foreach (var state in states)
            {
                var token = state[SaveKey];
                if (token == null) continue;
                if (!(token is JObject fuel)) return MigrationSaveJson.Reject($"{SaveKey} がオブジェクトではありません。 type={token.Type}", out failureReason);

                // GUID欠損と種別なしだけを補正し、未知種別や不正GUIDは厳格検査へ残す
                // Repair absent identity/type only; leave unknown types and malformed GUIDs to strict restore
                var type = fuel["activeFuelType"];
                if (MigrationSaveJson.IsAbsentOrNull(type) ||
                    (type.Type == JTokenType.String && ((string)type == "Item" && IsEmpty(fuel["currentFuelItemGuid"]) ||
                    (string)type == "Fluid" && IsEmpty(fuel["currentFuelFluidGuid"])))) _targets.Add(fuel);
            }
            return true;

            #region Internal
            bool IsEmpty(JToken value) => MigrationSaveJson.IsAbsentOrNull(value) ||
                value.Type == JTokenType.String && (string.IsNullOrEmpty((string)value) || (Guid.TryParse((string)value, out var guid) && guid == Guid.Empty));
            #endregion
        }

        public string ApplyAndSummarize()
        {
            foreach (var fuel in _targets)
            {
                fuel["activeFuelType"] = "None";
                fuel["remainingFuelSeconds"] = 0;
                fuel["currentFuelItemGuid"] = JValue.CreateNull();
                fuel["currentFuelFluidGuid"] = JValue.CreateNull();
            }
            return $"識別不能な旧燃焼を停止した発電機={_targets.Count}台";
        }
    }
}
