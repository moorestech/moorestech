using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.LevelFamiliesModule;
using Mooresmaster.Model.LevelFamiliesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // レベルファミリー定義(levelFamilies.json)を保持し、基準ItemId＋レベル番号から変種ItemIdを解決するマスタ
    // Master that holds level family definitions and resolves variant ItemIds from a base ItemId + level number
    public class LevelFamilyMaster : IMasterValidator
    {
        public readonly LevelFamilies Families;

        // baseItemId → レベル順の変種ItemId配列（index 0 = レベル1）
        // baseItemId → level-ordered variant ItemId array (index 0 = level 1)
        private Dictionary<ItemId, ItemId[]> _variantTable;

        public LevelFamilyMaster(JToken jToken)
        {
            Families = LevelFamiliesLoader.Load(jToken);
        }

        public bool HasFamily(ItemId baseItemId)
        {
            return _variantTable.ContainsKey(baseItemId);
        }

        public ItemId GetVariantItemId(ItemId baseItemId, int level)
        {
            // レベルは1始まり。範囲外は[1, 最大レベル]へクランプする
            // Levels are 1-based; out-of-range values are clamped into [1, max level]
            var variants = _variantTable[baseItemId];
            var index = Math.Clamp(level - 1, 0, variants.Length - 1);
            return variants[index];
        }

        public ItemId GetMaxLevelItemId(ItemId baseItemId)
        {
            var variants = _variantTable[baseItemId];
            return variants[variants.Length - 1];
        }

        public bool Validate(out string errorLogs)
        {
            return LevelFamilyMasterUtil.Validate(Families, out errorLogs);
        }

        public void Initialize()
        {
            LevelFamilyMasterUtil.Initialize(Families, out _variantTable);
        }
    }
}
