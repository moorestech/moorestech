using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
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
        private readonly IUiOsCursor _osCursor;
        private IReadOnlyList<Guid> _categories = Array.Empty<Guid>();
        private ModeSwitch<Guid> _switch;
        private IDisposable _switchSubscription;

        public IObservable<Guid> OnCategorySelected => _onCategorySelected;

        public BuildMenuCategorySidebar(IUiOsCursor osCursor)
        {
            _osCursor = osCursor;
            AddToClassList("build-menu__sidebar");
        }

        public void Render(BuildMenuShownList shown, BuildMenuViewModel viewModel)
        {
            if (!_categories.SequenceEqual(shown.SidebarCategories)) Rebuild();

            // ヒット無しカテゴリだけ無効
            // Only categories without hits are disabled (the Web disabled)
            foreach (var category in _categories) _switch.SetOptionEnabled(category, shown.IsCategoryEnabled(category));

            #region Internal

            void Rebuild()
            {
                _categories = shown.SidebarCategories.ToList();
                _switchSubscription?.Dispose();
                _switch?.RemoveFromHierarchy();

                // 選択肢の文は VM の見出しの名前。高さのつまみは画面のクラスで上書きする（D-26）
                // Option labels are the view model's heading names; the height knob is overridden through the screen class (D-26)
                var options = _categories.Select(guid => new ModeSwitchOption<Guid>($"build-menu-category-{guid:D}", guid, viewModel.HeadingNameOf(guid))).ToList();
                _switch = new ModeSwitch<Guid>("build-menu-sidebar", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, options, _osCursor);

                _switch.AddToClassList("build-menu__category-switch");
                _switchSubscription = _switch.OnOptionSelected.Subscribe(_onCategorySelected.OnNext);
                Add(_switch);
            }

            #endregion
        }

        public void SetActive(Guid? categoryGuid)
        {
            // silent-ok: 項目を作る前の現在地は捨てる / BuildMenuView が Render の直後に今の現在地で SetActive を呼び直す（Rebuild で消えた選択も戻す）
            // silent-ok: an active category before items exist is dropped / BuildMenuView calls SetActive with the current category right after Render (also restoring a selection lost to Rebuild)
            if (_switch == null) return;
            if (categoryGuid is { } guid && _categories.Contains(guid)) _switch.SetSelected(guid);
            else _switch.SetSelectedNone();
        }
    }
}
