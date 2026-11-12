using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 抽選の中身欄: 失敗率またはダイシング品質
    /// Draw-detail section: fail rate or dicing tier, loss and level shares; fetch-state line only while unavailable
    /// </summary>
    public sealed class CleanRoomYieldSectionView : VisualElement
    {
        private readonly BlockInfoUnavailableLine _unavailable = new("clean-room-yield-fetch-state", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldInfoLabel);
        private readonly ContextRowList _section = new("clean-room-yield-section", UiGap.Px2);
        private readonly List<LocalizedLabel> _labels = new();

        public CleanRoomYieldSectionView()
        {
            name = "clean-room-yield-section-view";
        }

        public void Render(SectionContent<CleanRoomYieldFetched> entry)
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
                Debug.LogError("[CleanRoomYieldSectionView] Render called with an absent entry; the caller must hide absent sections");
                return;
            }
            var lines = CleanRoomYieldLogic.Lines(detail);
            var rows = new List<VisualElement>();
            for (var i = 0; i < lines.Count; i++)
            {
                // 行数だけラベルを使い回し毎回差す
                // Reuse labels per line and set text each time
                if (i == _labels.Count) _labels.Add(NewLabel());
                _labels[i].name = lines[i].RowId;
                _labels[i].SetText(lines[i].Text);
                rows.Add(_labels[i]);
            }
            _section.SetRows(rows);
            ContextRowList.MountOnly(this, _section.Stack);

            #region Internal

            LocalizedLabel NewLabel()
            {
                var label = new LocalizedLabel();
                label.AddToClassList(UiTypographyClass.TextXs);
                label.AddToClassList(UiTypographyClass.TextDefault);
                return label;
            }

            #endregion
        }
    }
}
