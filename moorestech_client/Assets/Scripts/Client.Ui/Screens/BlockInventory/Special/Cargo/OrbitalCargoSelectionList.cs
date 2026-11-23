using System;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Text;
using Core.Master;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の一覧。先頭に状態文、続けて解放済みの種類の行（前例 MachineRecipeSelectionList）
    /// The cargo list: the status line first, then rows of unlocked kinds (precedent MachineRecipeSelectionList)
    /// </summary>
    public sealed class OrbitalCargoSelectionList : VisualElement
    {
        private readonly string _testPrefix;
        private readonly GameContentPresentation _content;
        private readonly IUiOsCursor _osCursor;
        private readonly RecipeListScrollView _list;
        private readonly LocalizedLabel _status;
        private readonly Subject<ItemId> _rowPressed = new();
        private readonly CompositeDisposable _rowSubscriptions = new();
        private readonly List<ItemId> _rowKinds = new();
        private readonly List<OrbitalCargoSelectionRowView> _rows = new();

        public IObservable<ItemId> OnRowPressed => _rowPressed;

        public OrbitalCargoSelectionList(string testPrefix, GameContentPresentation content, IUiOsCursor osCursor)
        {
            pickingMode = PickingMode.Ignore;
            _testPrefix = testPrefix;
            _content = content;
            _osCursor = osCursor;
            AddToClassList("orbital-cargo-list");
            _list = new RecipeListScrollView($"{testPrefix}-cargo-list");
            _status = SpecialViewParts.Label($"{testPrefix}-cargo-status", UiTypographyClass.TextSm, UiTypographyClass.TextHighContrast);
            _status.pickingMode = PickingMode.Ignore;
            _list.SetLeadingContent(_status);
            Add(_list);
        }

        public void Render(OrbitalCargoDetail cargo, UiText status)
        {
            _status.SetText(status);
            // 種類の並びが変わったときだけ行を作り直す（状態更新ごとに作らない。D-37）
            // Rebuild rows only when the kinds change, not on every state update (D-37)
            if (!SameKinds()) Rebuild();
            for (var i = 0; i < _rows.Count; i++) _rows[i].SetSelected(cargo.IsSelectedChoice(_rowKinds[i]));

            #region Internal

            bool SameKinds()
            {
                if (_rowKinds.Count != cargo.SelectableChoices.Count) return false;
                for (var i = 0; i < _rowKinds.Count; i++)
                {
                    if (_rowKinds[i] != cargo.SelectableChoices[i].PayloadItemId) return false;
                }
                return true;
            }

            void Rebuild()
            {
                // 古い行の押下を切り離してから新しい行へつなぎ直す
                // Disconnect old row presses before wiring the new rows
                _rowSubscriptions.Clear();
                _rowKinds.Clear();
                _rows.Clear();
                var entries = new List<VisualElement>(cargo.SelectableChoices.Count);
                foreach (var choice in cargo.SelectableChoices)
                {
                    var row = new OrbitalCargoSelectionRowView($"{_testPrefix}-cargo-row-{choice.PayloadItemId.AsPrimitive()}", choice, cargo.FlightSeconds, _content, _osCursor);
                    row.OnPressed.Subscribe(_rowPressed.OnNext).AddTo(_rowSubscriptions);
                    _rowKinds.Add(choice.PayloadItemId);
                    _rows.Add(row);
                    entries.Add(row);
                }
                _list.SetEntries(entries);
            }

            #endregion
        }
    }
}
