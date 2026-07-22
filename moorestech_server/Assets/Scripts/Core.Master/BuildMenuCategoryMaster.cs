using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.BuildMenuModule;
using Mooresmaster.Model.BuildMenuModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class BuildMenuCategoryMaster : IMasterValidator
    {
        public readonly BuildMenuCategoryElement[] Categories;
        public readonly ReplaceFamilyElement[] ReplaceFamilies;

        private HashSet<(string category, string subCategory)> _definedPairs;
        private Dictionary<string, (string category, string subCategory)> _pairByEntrySource;

        public BuildMenuCategoryMaster(JToken buildMenuJToken)
        {
            var buildMenu = BuildMenuLoader.Load(buildMenuJToken);
            Categories = buildMenu.Categories;
            ReplaceFamilies = buildMenu.ReplaceFamilies;
        }

        public bool Validate(out string errorLogs)
        {
            // カテゴリ/サブカテゴリ名の一意性検証
            // Validate uniqueness of category and sub category names
            errorLogs = string.Empty;
            var categoryNames = Categories.Select(c => c.Name).ToList();
            foreach (var duplicated in categoryNames.GroupBy(n => n).Where(g => 1 < g.Count()))
                errorLogs += $"[BuildMenuCategoryMaster] duplicate category name:{duplicated.Key}\n";

            foreach (var category in Categories)
            foreach (var duplicated in category.SubCategories.Select(s => s.Name).GroupBy(n => n).Where(g => 1 < g.Count()))
                errorLogs += $"[BuildMenuCategoryMaster] duplicate subCategory:{duplicated.Key} in category:{category.Name}\n";

            // blocks以外のentrySourceは行き先が一意になるよう「ちょうど1箇所」の定義を要求する
            // Each non-blocks entrySource must be defined exactly once so its entries have a unique destination
            var nonBlockSources = new[]
            {
                BuildMenuSubCategoryElement.EntrySourceConst.trainCars,
                BuildMenuSubCategoryElement.EntrySourceConst.connectTools,
                BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool,
                BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints,
            };
            var sourceCounts = Categories.SelectMany(c => c.SubCategories).GroupBy(s => s.EntrySource).ToDictionary(g => g.Key, g => g.Count());
            foreach (var source in nonBlockSources)
            {
                var count = sourceCounts.GetValueOrDefault(source);
                if (count != 1) errorLogs += $"[BuildMenuCategoryMaster] entrySource:{source} must be defined exactly once but found {count}\n";
            }

            return errorLogs == string.Empty;
        }

        public void Initialize()
        {
            // 参照整合チェックとentrySource逆引き用の索引を構築
            // Build lookups for reference validation and entrySource resolution
            _definedPairs = new HashSet<(string, string)>();
            _pairByEntrySource = new Dictionary<string, (string, string)>();
            foreach (var category in Categories)
            foreach (var subCategory in category.SubCategories)
            {
                _definedPairs.Add((category.Name, subCategory.Name));
                if (subCategory.EntrySource != BuildMenuSubCategoryElement.EntrySourceConst.blocks)
                    _pairByEntrySource[subCategory.EntrySource] = (category.Name, subCategory.Name);
            }
        }

        public bool Contains(string category, string subCategory)
        {
            return _definedPairs.Contains((category, subCategory));
        }

        public (string category, string subCategory) GetPairByEntrySource(string entrySource)
        {
            return _pairByEntrySource[entrySource];
        }
    }
}
