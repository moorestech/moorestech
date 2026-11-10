using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 抽選の中身欄: 工程の失敗率、またはダイシングの品質 Tier・損失率・レベル別割合（Web CleanRoomYieldSection）。取れていない間は取得状態の 1 行だけ
    /// Draw-detail section: a process's fail rate, or dicing tier, loss and level shares (Web CleanRoomYieldSection); only the fetch-state line while unavailable
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

        public void Render(ContextEntry<CleanRoomYieldFetched> entry)
        {
            if (entry is UnavailableContextEntry<CleanRoomYieldFetched> unavailable)
            {
                _unavailable.Render(unavailable.FetchState);
                ContextRowList.MountOnly(this, _unavailable);
                return;
            }
            var lines = CleanRoomYieldLogic.Lines(((FetchedContextEntry<CleanRoomYieldFetched>)entry).Value);
            var rows = new List<VisualElement>();
            for (var i = 0; i < lines.Count; i++)
            {
                // 行の数だけラベルを使い回し、名前と文を毎回差す
                // Reuse labels per line index, setting the name and text each time
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
