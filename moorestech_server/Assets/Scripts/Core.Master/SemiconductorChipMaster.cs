using System;
using System.Collections.Generic;
using Mooresmaster.Loader.SemiconductorChipsModule;
using Mooresmaster.Model.SemiconductorChipsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // 半導体チップ Lv↔ItemId 対応とレシピ出力要素単位のレベル分布を管理。
    // Manages semiconductor chip Lv<->ItemId mapping and per-output-element level distributions.
    public class SemiconductorChipMaster : IMasterValidator
    {
        private readonly SemiconductorChips _data;

        // Lv→ItemId の正引きテーブル（ロード時1回構築）。
        // Forward lookup table Lv->ItemId, built once on load.
        private Dictionary<int, ItemId> _levelToItemId;

        // ItemId→Lv の逆引きテーブル（チップ判定用）。
        // Reverse lookup table ItemId->Lv (for chip detection).
        private Dictionary<ItemId, int> _itemIdToLevel;

        // (machineRecipeGuid, outputItemGuid) → levelWeights の分布テーブル。
        // Distribution table keyed by (machineRecipeGuid, outputItemGuid).
        private Dictionary<(Guid, Guid), IReadOnlyList<(int level, double weight)>> _distributions;

        public SemiconductorChipMaster(JToken jToken)
        {
            _data = SemiconductorChipsLoader.Load(jToken);
        }

        // チップ Lv から ItemId を解決する。Lv が存在しない場合は KeyNotFoundException を投げる。
        // Resolve ItemId from chip level; throws KeyNotFoundException if level is absent.
        public ItemId GetChipItemId(int level)
        {
            return _levelToItemId[level];
        }

        // ItemId からチップ Lv を逆引きする。チップでなければ -1 を返す。
        // Reverse-lookup chip level from ItemId; returns -1 if not a chip.
        public int GetChipLevel(ItemId itemId)
        {
            return _itemIdToLevel.TryGetValue(itemId, out var lv) ? lv : -1;
        }

        // レシピ出力要素の分布を取得する。level 昇順にソートして返す。分布が無ければ false。
        // Get distribution for a recipe output element, sorted ascending by level. Returns false if absent.
        public bool TryGetDistribution(Guid machineRecipeGuid, Guid outputItemGuid, out IReadOnlyList<(int level, double weight)> dist)
        {
            return _distributions.TryGetValue((machineRecipeGuid, outputItemGuid), out dist);
        }

        public bool Validate(out string errorLogs)
        {
            // chipLevels の重複チェック。
            // Validate chipLevels for duplicates.
            var seenLevels = new HashSet<int>();
            var seenGuids = new HashSet<Guid>();
            foreach (var chip in _data.ChipLevels)
            {
                if (!seenLevels.Add(chip.Level))
                {
                    errorLogs = $"semiconductorChips: duplicate level {chip.Level}";
                    return false;
                }
                if (!seenGuids.Add(chip.ItemGuid))
                {
                    errorLogs = $"semiconductorChips: duplicate itemGuid {chip.ItemGuid}";
                    return false;
                }
            }

            // outputDistributions の levelWeights が空でないことを確認。
            // Ensure every outputDistributions entry has at least one levelWeight.
            foreach (var dist in _data.OutputDistributions)
            {
                if (dist.LevelWeights.Length == 0)
                {
                    errorLogs = $"semiconductorChips: outputDistributions entry has empty levelWeights (recipe={dist.MachineRecipeGuid})";
                    return false;
                }
            }

            errorLogs = null;
            return true;
        }

        public void Initialize()
        {
            // Lv↔ItemId テーブルを構築する。
            // Build Lv<->ItemId tables.
            _levelToItemId = new Dictionary<int, ItemId>(_data.ChipLevels.Length);
            _itemIdToLevel = new Dictionary<ItemId, int>(_data.ChipLevels.Length);
            foreach (var chip in _data.ChipLevels)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(chip.ItemGuid);
                _levelToItemId[chip.Level] = itemId;
                _itemIdToLevel[itemId] = chip.Level;
            }

            // 分布テーブルを level 昇順ソートで構築する。
            // Build distribution table with per-entry list sorted ascending by level.
            _distributions = new Dictionary<(Guid, Guid), IReadOnlyList<(int level, double weight)>>(_data.OutputDistributions.Length);
            foreach (var dist in _data.OutputDistributions)
            {
                var weights = new List<(int level, double weight)>(dist.LevelWeights.Length);
                foreach (var lw in dist.LevelWeights)
                    weights.Add((lw.Level, lw.Weight));

                // level 昇順ソート（Task 1 の DrawBaseLevel が前提とする順序）。
                // Sort ascending by level (required by Task 1's DrawBaseLevel).
                weights.Sort((a, b) => a.level.CompareTo(b.level));

                _distributions[(dist.MachineRecipeGuid, dist.OutputItemGuid)] = weights;
            }
        }
    }
}
