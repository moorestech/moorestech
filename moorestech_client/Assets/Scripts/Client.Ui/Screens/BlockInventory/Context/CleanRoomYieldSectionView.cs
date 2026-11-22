using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 確率の欄: 最終の確率の行（露光は失敗、ダイシングは出力・Lv・失敗）と、改善の行があるときの「失敗率の改善方法」ボタン
    /// Odds section: final-odds rows (failure for exposure; output, levels and failure for dicing) plus the improvement button when rows exist
    /// </summary>
    public sealed class CleanRoomYieldSectionView : VisualElement
    {
        private readonly BlockInfoUnavailableLine _unavailable = new("clean-room-yield-fetch-state", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldInfoLabel);
        private readonly ContextRowList _section = new("clean-room-yield-section", UiGap.Px2);
        private readonly List<LocalizedLabel> _labels = new();
        private readonly PanelActionButton _improvementOpen;

        public CleanRoomYieldSectionView(IUiOsCursor osCursor, ICleanRoomYieldImprovementCommands improvementCommands)
        {
            name = "clean-room-yield-section-view";

            // 押下は View が直接開く口へ渡す（Presenter は中継しない。SpaceElevatorSectionView と同じ）
            // The press goes straight from the view to the open entry (no presenter relay; same as SpaceElevatorSectionView)
            _improvementOpen = new PanelActionButton("clean-room-yield-improvement-open", new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImprovementOpen), osCursor);
            _improvementOpen.OnClicked.Subscribe(_ => improvementCommands.Open());
        }

        public void Render(SectionContent<CleanRoomYieldOdds> entry)
        {
            if (entry.TryGetUnavailable(out var fetchState))
            {
                _unavailable.RenderInto(this, fetchState);
                return;
            }
            // 取得不可でも在るでもない（欄が無い）まま届くのは結線の誤りなので知らせて戻る
            // Neither unavailable nor present (absent) means a wiring mistake, so report it and return
            if (!entry.TryGetPresent(out var odds))
            {
                Debug.LogError("[CleanRoomYieldSectionView] Render called with an absent entry; the caller must hide absent sections");
                return;
            }
            var lines = CleanRoomYieldLogic.Lines(odds);
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

            // 改善の行が1つ以上あるときだけ最後の行にボタンを置く（ADR 0105 §4）
            // Put the button as the last row only when at least one improvement row exists (ADR 0105 §4)
            if (odds.HasImprovements) rows.Add(_improvementOpen);
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
