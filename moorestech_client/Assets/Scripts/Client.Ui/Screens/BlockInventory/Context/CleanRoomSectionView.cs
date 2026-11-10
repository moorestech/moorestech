using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 部屋欄: クラス・空気・清浄機・停止理由の順
    /// Room section: class, air, purifiers, halt reason in order (Web CleanRoomSection); only the fetch-state line while unavailable
    /// </summary>
    public sealed class CleanRoomSectionView : VisualElement
    {
        private readonly BlockInfoUnavailableLine _unavailable = new("clean-room-fetch-state", LocalizationKeys.Ui.BlockInventory.CleanRoomInfoLabel);
        private readonly ContextRowList _section = new("clean-room-section", UiGap.Px2);
        private readonly LackHighlightText _headline = new("clean-room-headline", LackHighlightTextSize.Sm);
        private readonly LocalizedLabel _air = new() { name = "clean-room-air" };
        private readonly List<LocalizedLabel> _filters = new();
        private readonly LackHighlightText _noFilter = new("clean-room-no-filter", LackHighlightTextSize.Xs);
        private readonly LackHighlightText _haltReason = new("clean-room-halt-reason", LackHighlightTextSize.Sm);

        public CleanRoomSectionView()
        {
            name = "clean-room-section-view";
            _air.AddToClassList(UiTypographyClass.TextSm);
            _air.AddToClassList(UiTypographyClass.TextDefault);
        }

        public void Render(ContextEntry<CleanRoomFetchedDetail> entry)
        {
            if (entry is UnavailableContextEntry<CleanRoomFetchedDetail> unavailable)
            {
                _unavailable.RenderInto(this, unavailable.FetchState);
                return;
            }
            RenderFetched(((FetchedContextEntry<CleanRoomFetchedDetail>)entry).Value);
            ContextRowList.MountOnly(this, _section.Stack);

            #region Internal

            void RenderFetched(CleanRoomFetchedDetail detail)
            {
                var display = CleanRoomLogic.Display(detail);
                var rows = new List<VisualElement>();

                // 赤は不足のときだけ。空気は密閉時のみ
                // Red only when insufficient; air and purifier rows exist only for an enclosed room
                _headline.Render(display.Headline, display.HeadlineInsufficient);
                rows.Add(_headline);
                if (display is EnclosedCleanRoomDisplay enclosed)
                {
                    _air.SetText(enclosed.Air);
                    rows.Add(_air);
                    AddFilterRows(enclosed, rows);
                    if (enclosed.ShowsNoFilter)
                    {
                        _noFilter.Render(enclosed.NoFilter, true);
                        rows.Add(_noFilter);
                    }
                }

                // 止まっていなければ停止理由の行は無い
                // No halt-reason row while not halted
                if (display.TryGetHaltReason(out var haltReason))
                {
                    _haltReason.Render(haltReason, true);
                    rows.Add(_haltReason);
                }
                _section.SetRows(rows);
            }

            void AddFilterRows(EnclosedCleanRoomDisplay source, List<VisualElement> rows)
            {
                for (var i = 0; i < source.Filters.Count; i++)
                {
                    if (i == _filters.Count) _filters.Add(NewFilterLabel(i));
                    _filters[i].SetText(source.Filters[i]);
                    rows.Add(_filters[i]);
                }
            }

            LocalizedLabel NewFilterLabel(int index)
            {
                var label = new LocalizedLabel { name = $"clean-room-air-filter-{index}" };
                label.AddToClassList(UiTypographyClass.TextXs);
                label.AddToClassList(UiTypographyClass.TextDefault);
                return label;
            }

            #endregion
        }
    }
}
