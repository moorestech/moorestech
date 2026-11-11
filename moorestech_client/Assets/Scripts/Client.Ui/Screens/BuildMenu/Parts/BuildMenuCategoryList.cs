using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Decor;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 全カテゴリ群と末尾スペーサ（Web BuildMenuCategoryList の写し）。構造が変わらなければ組み直さない
    /// All category groups plus the trailing spacer (port of the Web BuildMenuCategoryList); unchanged structure is not rebuilt
    /// </summary>
    public sealed class BuildMenuCategoryList : VisualElement
    {
        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;
        private readonly IBuildMenuEntryHandler _handler;
        private readonly VisualElement _groupsRoot = new();
        private readonly Dictionary<Guid, VisualElement> _headings = new();
        private readonly Dictionary<Guid, VisualElement> _groups = new();
        private readonly List<BuildMenuSectionView> _sectionViews = new();
        private readonly Subject<Unit> _groupGeometryChanged = new();
        // null は「まだ描いていない」。空の文字列は「群が 0 の構造」で、別の意味
        // null means "not rendered yet"; the empty string is "a structure with no groups", a different meaning
        private string _structureKey;

        public VisualElement Spacer { get; } = new();

        // どれかの群の寸法が変わった（Web は群ごとに ResizeObserver。全体の高さが同じまま配分だけ変わる場合も拾う。D-9）
        // Some group's size changed (the Web observes each group; catches redistribution with an unchanged total height, D-9)
        public IObservable<Unit> OnGroupGeometryChanged => _groupGeometryChanged;

        public BuildMenuCategoryList(GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler)
        {
            _content = content;
            _icons = icons;
            _handler = handler;
            AddToClassList("build-menu__list");
            name = "build-menu-sections";
            Add(_groupsRoot);
            Spacer.name = "build-menu-trailing-spacer";
            Spacer.AddToClassList("build-menu__trailing-spacer");
            Spacer.pickingMode = PickingMode.Ignore;
            Add(Spacer);
        }

        public void Render(IReadOnlyList<BuildMenuCategoryGroup> groups, BuildMenuViewModel viewModel)
        {
            // 同じ構造（群・節・エントリの並び）なら中身だけ差し替え、スクロール位置と hover を保つ
            // For the same structure (groups, sections, entry order) only swap contents, keeping scroll and hover
            var sections = groups.SelectMany(group => group.Sections).ToList();
            var key = string.Join("|", sections.SelectMany(section => section.Entries.Select(entry => $"{section.CategoryGuid}/{section.SubCategoryGuid}/{entry.TargetId}")));
            if (key == _structureKey)
            {
                for (var i = 0; i < _sectionViews.Count; i++) _sectionViews[i].Render(sections[i], viewModel);
                return;
            }
            _structureKey = key;
            Rebuild(groups, viewModel);
        }

        public void SetSpacerHeight(float height)
        {
            Spacer.style.minHeight = height;
        }

        public bool TryGetHeading(Guid categoryGuid, out VisualElement heading)
        {
            return _headings.TryGetValue(categoryGuid, out heading);
        }

        public bool TryGetGroup(Guid categoryGuid, out VisualElement group)
        {
            return _groups.TryGetValue(categoryGuid, out group);
        }

        private void Rebuild(IReadOnlyList<BuildMenuCategoryGroup> groups, BuildMenuViewModel viewModel)
        {
            _groupsRoot.Clear();
            _headings.Clear();
            _groups.Clear();
            _sectionViews.Clear();

            // 群同士の間隔は 2 群目以降とスペーサの上の余白で写す（UITK に gap が無い）
            // Gaps between groups become top margins on later groups and the spacer (UITK has no gap)
            for (var i = 0; i < groups.Count; i++) _groupsRoot.Add(CreateGroup(groups[i], i > 0));
            Spacer.EnableInClassList("build-menu__trailing-spacer--following", groups.Count > 0);

            #region Internal

            VisualElement CreateGroup(BuildMenuCategoryGroup group, bool following)
            {
                var root = new VisualElement { name = $"build-menu-category-{group.CategoryGuid:D}-group" };
                root.AddToClassList("build-menu__group");
                root.RegisterCallback<GeometryChangedEvent>(_ => _groupGeometryChanged.OnNext(Unit.Default));
                root.EnableInClassList("build-menu__group--following", following);
                var heading = new LocalizedLabel { name = $"build-menu-category-heading-{group.CategoryGuid:D}" };
                heading.AddToClassList("build-menu__category-heading");
                heading.SetText(viewModel.HeadingNameOf(group.CategoryGuid));
                root.Add(heading);
                var rule = new FadeRule();
                rule.AddToClassList("build-menu__stacked");
                root.Add(rule);

                // 節は 6px の積みに、2 節目からさらに 6px を足す（Web .section + .section）
                // Sections stack with 6px, and later sections add another 6px (Web .section + .section)
                for (var s = 0; s < group.Sections.Count; s++)
                {
                    var view = new BuildMenuSectionView(group.Sections[s], viewModel, _content, _icons, _handler);
                    view.AddToClassList("build-menu__stacked");
                    view.EnableInClassList("build-menu__section--following", s > 0);
                    _sectionViews.Add(view);
                    root.Add(view);
                }
                _headings[group.CategoryGuid] = heading;
                _groups[group.CategoryGuid] = root;
                return root;
            }

            #endregion
        }
    }
}
