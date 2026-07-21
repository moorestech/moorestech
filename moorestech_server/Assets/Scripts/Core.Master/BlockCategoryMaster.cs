using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.BlockCategoriesModule;
using Mooresmaster.Model.BlockCategoriesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class BlockCategoryMaster : IMasterValidator
    {
        public readonly BlockCategories BlockCategories;

        private HashSet<(string category, string subCategory)> _definedPairs;

        public BlockCategoryMaster(JToken jToken)
        {
            BlockCategories = BlockCategoriesLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            // カテゴリ/サブカテゴリ名の一意性検証
            // Validate uniqueness of category and sub category names
            errorLogs = string.Empty;
            var categoryNames = BlockCategories.Data.Select(c => c.Name).ToList();
            foreach (var duplicated in categoryNames.GroupBy(n => n).Where(g => 1 < g.Count()))
                errorLogs += $"[BlockCategoryMaster] duplicate category name:{duplicated.Key}\n";

            foreach (var category in BlockCategories.Data)
            foreach (var duplicated in category.SubCategories.Select(s => s.Name).GroupBy(n => n).Where(g => 1 < g.Count()))
                errorLogs += $"[BlockCategoryMaster] duplicate subCategory:{duplicated.Key} in category:{category.Name}\n";

            return errorLogs == string.Empty;
        }

        public void Initialize()
        {
            // 参照整合チェック用に全ペアを索引化
            // Build a lookup of all pairs for reference validation
            _definedPairs = new HashSet<(string, string)>();
            foreach (var category in BlockCategories.Data)
            foreach (var subCategory in category.SubCategories)
                _definedPairs.Add((category.Name, subCategory.Name));
        }

        public bool Contains(string category, string subCategory)
        {
            return _definedPairs.Contains((category, subCategory));
        }
    }
}
