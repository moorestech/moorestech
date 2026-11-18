using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.Text;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// ビルドメニューの不変の ViewModel（マスタ順のカテゴリ・見出しの名前・解放済みのエントリ）
    /// Immutable build-menu view model: categories in master order, heading names and the unlocked entries
    /// </summary>
    public sealed class BuildMenuViewModel
    {
        public static readonly BuildMenuViewModel Empty = new(Array.Empty<BuildMenuCategory>(), new Dictionary<Guid, UiText>(), Array.Empty<BuildMenuEntryViewModel>());

        public IReadOnlyList<BuildMenuCategory> Categories { get; }
        public IReadOnlyList<BuildMenuCatalogEntry> CatalogEntries { get; }

        private readonly IReadOnlyDictionary<Guid, UiText> _headingNames;
        private readonly Dictionary<Guid, BuildMenuEntryViewModel> _entriesById;

        public BuildMenuViewModel(IReadOnlyList<BuildMenuCategory> categories, IReadOnlyDictionary<Guid, UiText> headingNames, IReadOnlyList<BuildMenuEntryViewModel> entries)
        {
            Categories = categories;
            CatalogEntries = entries.Select(entry => entry.Catalog).ToList();
            _headingNames = headingNames;
            _entriesById = entries.ToDictionary(entry => entry.Catalog.TargetId);
        }

        // カテゴリとサブカテゴリの見出しの名前。無い Guid は組み立ての誤りなので例外で止める
        // Heading name of a category or sub-category; a missing Guid is a construction bug, so it throws
        public UiText HeadingNameOf(Guid categoryOrSubCategoryGuid)
        {
            return _headingNames[categoryOrSubCategoryGuid];
        }

        public bool TryGetEntry(Guid targetId, out BuildMenuEntryViewModel entry)
        {
            return _entriesById.TryGetValue(targetId, out entry);
        }
    }
}
