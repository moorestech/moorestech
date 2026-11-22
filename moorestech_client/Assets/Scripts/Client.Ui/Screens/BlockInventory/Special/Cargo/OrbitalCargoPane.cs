using System;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Text;
using Core.Master;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 一覧を出すときは積荷の一覧、出さないときは見出し＋持ち主の中身を置く View（前例 MachineSectionView のモード切替）。一覧を出すかは Presenter が決めて渡す
    /// View placing the cargo list when told to, otherwise the headline plus the owner's content (precedent MachineSectionView's mode switch); the presenter decides and passes the mode
    /// </summary>
    public sealed class OrbitalCargoPane : VisualElement
    {
        private readonly OrbitalCargoSelectionList _list;
        private readonly OrbitalCargoHeader _header;
        private readonly UiStack _selected = new(UiGap.Sm);
        private readonly VisualElement _selectedHost;
        private bool _hasMode;
        private bool _showsSelection;

        public IObservable<ItemId> OnRowPressed => _list.OnRowPressed;
        public IObservable<Unit> OnChangeCargoRequested => _header.OnPressed;

        public OrbitalCargoPane(string testPrefix, SpecialViewKit kit, VisualElement selectedContent, bool fillsPanelHeight)
        {
            name = $"{testPrefix}-cargo-pane";
            AddToClassList("orbital-cargo-pane");
            EnableInClassList("orbital-cargo-pane--fill-panel-height", fillsPanelHeight);
            pickingMode = PickingMode.Ignore;
            _list = new OrbitalCargoSelectionList(testPrefix, kit.Presentation, kit.OsCursor);
            _header = new OrbitalCargoHeader(testPrefix, kit.Presentation, kit.OsCursor);
            _selected.pickingMode = PickingMode.Ignore;
            _selected.AddItem(_header);
            _selected.AddItem(selectedContent);
            _selectedHost = fillsPanelHeight ? WrapInScroll() : _selected;

            // 行の押下は View が直接コマンドへ渡す（前例 MachineSectionView・D-33）。一覧の開閉は Presenter が同じ押下を購読して進める
            // Row presses go straight from the view to the command (precedent MachineSectionView, D-33); the presenter subscribes to the same press to advance the list state
            _list.OnRowPressed.Subscribe(kit.Commands.SelectOrbitalCargo);

            #region Internal

            // 大型面では選んだ後の中身を縦のスクロール器に入れ、パネルの下端からはみ出さない
            // On the large panel the selected content sits in a vertical scroller so it never overflows the panel
            VisualElement WrapInScroll()
            {
                var scroll = new GameScrollView($"{testPrefix}-cargo-scroll", GameScrollAxis.Vertical);
                scroll.AddToClassList("orbital-cargo-pane__scroll");
                scroll.Add(_selected);
                return scroll;
            }

            #endregion
        }

        public void Render(OrbitalCargoDetail cargo, UiText listStatus, bool showsSelection)
        {
            // 木に置くのは今のモードの中身だけ。付け替えはモードが変わったときだけ（D-37）
            // Only the current mode's content is in the tree; re-attach only on a mode change (D-37)
            if (!_hasMode || showsSelection != _showsSelection)
            {
                _hasMode = true;
                _showsSelection = showsSelection;
                Clear();
                Add(showsSelection ? _list : _selectedHost);
                _selected.SetItemShown(_header, !showsSelection);
            }
            if (showsSelection)
            {
                _list.Render(cargo, listStatus);
                return;
            }
            _header.Render(cargo);
        }

        // 積荷の詳細が未着の間は持ち主の中身だけを出す（中身の側が取得待ちを語る。拠点の状態区画）
        // While the cargo detail is pending show only the owner's content, which shows its own loading line (the base's state section)
        public void RenderWithoutCargo()
        {
            _hasMode = false;
            Clear();
            Add(_selectedHost);
            _selected.SetItemShown(_header, false);
        }
    }
}
