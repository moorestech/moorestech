using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// ビルドメニューの群分けと検索（Web buildMenuGrouping.ts の写し）
    /// Build-menu grouping and search (port of the Web buildMenuGrouping.ts)
    /// </summary>
    public static class BuildMenuGrouping
    {
        // カテゴリ定義順→サブカテゴリ定義順で群にし、空の群は落とす。エントリの並びは配信順のまま
        // Group by category then sub-category definition order, dropping empty groups; entry order stays as delivered
        public static IReadOnlyList<BuildMenuCategoryGroup> Group(IReadOnlyList<BuildMenuCategory> categories, IReadOnlyList<BuildMenuCatalogEntry> entries)
        {
            var buckets = BucketByCategory();
            var groups = new List<BuildMenuCategoryGroup>();
            foreach (var category in categories)
            {
                // silent-ok: カテゴリとサブカテゴリの整合はマスタのロード時に検証済みで、ここで落ちるのは検索で空になった群だけ / 不整合はマスタロードの例外で開発者が気づく
                // Category consistency is validated at master load, so only search-emptied groups drop here; an inconsistency surfaces as a master-load exception
                if (!buckets.TryGetValue(category.CategoryGuid, out var subBuckets)) continue;
                var sections = new List<BuildMenuSection>();
                foreach (var subCategoryGuid in category.SubCategoryGuids)
                {
                    if (!subBuckets.TryGetValue(subCategoryGuid, out var sectionEntries)) continue;
                    sections.Add(new BuildMenuSection(category.CategoryGuid, subCategoryGuid, sectionEntries));
                }
                if (sections.Count == 0) continue;
                groups.Add(new BuildMenuCategoryGroup(category.CategoryGuid, sections));
            }
            return groups;

            #region Internal

            // 全カテゴリを常に載せるので、総当たりの再走査を避けて 1 パスでバケツに分ける
            // Every category stays mounted, so bucket the entries in one pass instead of rescanning per category
            Dictionary<Guid, Dictionary<Guid, List<BuildMenuCatalogEntry>>> BucketByCategory()
            {
                var byCategory = new Dictionary<Guid, Dictionary<Guid, List<BuildMenuCatalogEntry>>>();
                foreach (var entry in entries)
                {
                    if (!byCategory.TryGetValue(entry.CategoryGuid, out var bySubCategory))
                    {
                        bySubCategory = new Dictionary<Guid, List<BuildMenuCatalogEntry>>();
                        byCategory.Add(entry.CategoryGuid, bySubCategory);
                    }
                    if (!bySubCategory.TryGetValue(entry.SubCategoryGuid, out var bucket))
                    {
                        bucket = new List<BuildMenuCatalogEntry>();
                        bySubCategory.Add(entry.SubCategoryGuid, bucket);
                    }
                    bucket.Add(entry);
                }
                return byCategory;
            }

            #endregion
        }

        // 表示名の部分一致（大文字小文字を無視）。空の検索語は全件
        // Case-insensitive substring search on display labels; an empty query returns everything
        public static IReadOnlyList<BuildMenuCatalogEntry> Search(string query, IReadOnlyList<BuildMenuCatalogEntry> entries)
        {
            if (query.Length == 0) return entries;
            var lowered = query.ToLowerInvariant();
            return entries.Where(entry => entry.DisplayLabel.ToLowerInvariant().Contains(lowered)).ToList();
        }
    }
}
