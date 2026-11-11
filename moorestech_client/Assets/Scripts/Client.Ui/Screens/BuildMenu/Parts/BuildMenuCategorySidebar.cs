using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.State.BuildMenu.Entries;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 縦のカテゴリ切替（Web CategorySidebar の写し）。項目の並びが変わったときだけ ModeSwitch を作り直す
    /// Vertical category switch (port of the Web CategorySidebar); the ModeSwitch is rebuilt only when the item order changes
    /// </summary>
    public sealed class BuildMenuCategorySidebar : VisualElement
    {
        private readonly Subject<Guid> _onCategorySelected = new();
        private IReadOnlyList<Guid> _categories = Array.Empty<Guid>();
        private ModeSwitch<Guid> _switch;
        private IDisposable _switchSubscription;

        public IObservable<Guid> OnCategorySelected => _onCategorySelected;

        public BuildMenuCategorySidebar()
        {
            AddToClassList("build-menu__sidebar");
        }

        public void Render(BuildMenuShownList shown, BuildMenuViewModel viewModel)
        {
            if (!_categories.SequenceEqual(shown.SidebarCategories)) Rebuild(shown.SidebarCategories, viewModel);

            // ヒットの無いカテゴリだけ無効（Web の disabled）
            // Only categories without hits are disabled (the Web disabled)
            foreach (var category in _categories) _switch.SetOptionEnabled(category, shown.IsCategoryEnabled(category));
        }

        public void SetActive(Guid? categoryGuid)
        {
            // silent-ok: 項目を作る前の現在地は捨てる / 次の Render の後に ActiveCategory の購読がもう一度流す
            // silent-ok: an active category before items exist is dropped / the ActiveCategory subscription replays after the next Render
            if (_switch == null) return;
            if (categoryGuid is { } guid && _categories.Contains(guid)) _switch.SetSelected(guid);
            else _switch.SetSelectedNone();
        }

        private void Rebuild(IReadOnlyList<Guid> categories, BuildMenuViewModel viewModel)
        {
            _categories = categories.ToList();
            _switchSubscription?.Dispose();
            _switch?.RemoveFromHierarchy();

            // 選択肢の文は VM の見出しの名前。高さのつまみは画面のクラスで上書きする（D-26）
            // Option labels are the view model's heading names; the height knob is overridden through the screen class (D-26)
            var options = _categories.Select(guid => new ModeSwitchOption<Guid>($"build-menu-category-{guid:D}", guid, viewModel.HeadingNameOf(guid))).ToList();
            _switch = new ModeSwitch<Guid>("build-menu-sidebar", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, options);
            _switch.AddToClassList("build-menu__category-switch");
            _switchSubscription = _switch.OnOptionSelected.Subscribe(_onCategorySelected.OnNext);
            Add(_switch);
        }
    }
}
