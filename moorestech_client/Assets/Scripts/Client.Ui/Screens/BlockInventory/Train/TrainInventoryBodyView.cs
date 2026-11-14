using System;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Inventory;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車の本文（Web TrainInventoryBody の写し）。タブとスクロール器の中に持ち物と時刻表。タブの押下は Presenter の局所状態へ流す（D-33）
    /// Train body (copy of Web TrainInventoryBody): tabs, then inventory and timetable inside the scroll view; tab presses flow to the Presenter's local state (D-33)
    /// </summary>
    public sealed class TrainInventoryBodyView : VisualElement
    {
        private readonly ModeSwitch<TrainInventoryTab> _tabs;

        public TrainInventoryContentsView Contents { get; }
        public TrainTimetableAreaView Timetable { get; }
        public IObservable<TrainInventoryTab> OnTabSelected => _tabs.OnOptionSelected;

        public TrainInventoryBodyView(TrainInventoryContentsView contents, TrainTimetableAreaView timetable, IUiOsCursor osCursor)
        {
            name = "train-inventory-body";
            AddToClassList("train-body");
            Contents = contents;
            Timetable = timetable;
            _tabs = new ModeSwitch<TrainInventoryTab>("train-tabs", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Tablist, new[]
            {
                new ModeSwitchOption<TrainInventoryTab>("train-tab-inventory", TrainInventoryTab.Inventory, new UiText(LocalizationKeys.Ui.BlockInventory.TrainTabInventory)),
                new ModeSwitchOption<TrainInventoryTab>("train-tab-timetable", TrainInventoryTab.Timetable, new UiText(LocalizationKeys.Ui.BlockInventory.TrainTabTimetable)),
            }, osCursor);


            // 時刻表は持ち物タブのあいだも作ったまま隠す（Web の hidden。D-5）
            // The timetable stays built but hidden on the inventory tab (Web hidden; D-5)
            var scroll = new GameScrollView("train-body-scroll", GameScrollAxis.Vertical);
            scroll.AddToClassList("train-body__scroll");
            scroll.Add(Contents);
            scroll.Add(Timetable);
            Add(_tabs);
            Add(scroll);
        }

        public void RenderTab(TrainInventoryTab tab)
        {
            _tabs.SetSelected(tab);
            UiDisplay.SetShown(Contents, tab == TrainInventoryTab.Inventory);
            UiDisplay.SetShown(Timetable, tab == TrainInventoryTab.Timetable);
        }
    }
}
