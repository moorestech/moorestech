using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 部屋欄: 見出し・（清浄機のときだけ）台数・この清浄機・停止理由の順。取得できない間は取得状態の1行だけ
    /// Room section: headline, then (purifiers only) count and own status, then halt reason; only the fetch-state line while unavailable
    /// </summary>
    public sealed class CleanRoomSectionView : VisualElement
    {
        private readonly BlockInfoUnavailableLine _unavailable = new("clean-room-fetch-state", LocalizationKeys.Ui.BlockInventory.CleanRoomInfoLabel);
        private readonly ContextRowList _section = new("clean-room-section", UiGap.Px2);
        private readonly LackHighlightText _headline = new("clean-room-headline", LackHighlightTextSize.Sm);
        private readonly LocalizedLabel _airFilterCount = new() { name = "clean-room-air-filter-count", pickingMode = PickingMode.Ignore };
        private readonly LocalizedLabel _queriedAirFilter = new() { name = "clean-room-queried-air-filter", pickingMode = PickingMode.Ignore };
        private readonly LackHighlightText _haltReason = new("clean-room-halt-reason", LackHighlightTextSize.Sm);

        public CleanRoomSectionView()
        {
            name = "clean-room-section-view";
            _airFilterCount.AddToClassList(UiTypographyClass.TextSm);
            _airFilterCount.AddToClassList(UiTypographyClass.TextDefault);
            _queriedAirFilter.AddToClassList(UiTypographyClass.TextXs);
            _queriedAirFilter.AddToClassList(UiTypographyClass.TextDefault);
        }

        public void Render(SectionContent<CleanRoomFetchedDetail> entry)
        {
            if (entry.TryGetUnavailable(out var fetchState))
            {
                _unavailable.RenderInto(this, fetchState);
                return;
            }
            // 取得不可でも在るでもない（欄が無い）まま届くのは結線の誤りなので知らせて戻る
            // Neither unavailable nor present (absent) means a wiring mistake, so report it and return
            if (!entry.TryGetPresent(out var detail))
            {
                Debug.LogError("[CleanRoomSectionView] Render called with an absent entry; the caller must hide absent sections");
                return;
            }
            RenderFetched(detail);
            ContextRowList.MountOnly(this, _section.Stack);

            #region Internal

            void RenderFetched(CleanRoomFetchedDetail detail)
            {
                var display = CleanRoomLogic.Display(detail);
                var rows = new List<VisualElement>();

                // 赤は不足のときだけ。台数とこの清浄機は清浄機から見た部屋だけ
                // Red only when insufficient; count and own status only for the purifier's view
                _headline.Render(display.Headline, display.HeadlineInsufficient);
                rows.Add(_headline);
                if (display is AirFilterCleanRoomDisplay airFilter)
                {
                    _airFilterCount.SetText(airFilter.AirFilterCount);
                    rows.Add(_airFilterCount);
                    _queriedAirFilter.SetText(airFilter.QueriedAirFilter);
                    rows.Add(_queriedAirFilter);
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

            #endregion
        }
    }
}
