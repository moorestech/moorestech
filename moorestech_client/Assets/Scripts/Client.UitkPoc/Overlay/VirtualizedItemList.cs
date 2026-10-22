using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    // 1万行の仮想化ListViewと検索。絞り込みにかかった時間を表示し、自動スクロールで描画負荷も見る
    // 10k-row virtualized ListView with search; shows filter time and auto-scrolls to load rendering
    public class VirtualizedItemList : VisualElement
    {
        private readonly List<(string name, int amount)> _all = new();
        private readonly List<int> _visible = new();
        private readonly ListView _list;
        private readonly Label _status = new();
        private bool _autoScroll;

        public VirtualizedItemList(UitkPocContext context, List<string> itemNames, int rowCount)
        {
            AddToClassList("ov-vlist");
            var random = new System.Random(7);
            for (var i = 0; i < rowCount; i++) _all.Add(($"{itemNames[random.Next(itemNames.Count)]} #{i:00000}", random.Next(1, 9999)));
            _visible.AddRange(Enumerable.Range(0, rowCount));

            var search = new TextField("検索");
            search.RegisterValueChangedCallback(evt => Filter(evt.newValue));
            var autoScroll = new Toggle("自動スクロール");
            autoScroll.RegisterValueChangedCallback(evt => _autoScroll = evt.newValue);
            _status.AddToClassList("ov-vlist__status");
            Add(search);
            Add(autoScroll);
            Add(_status);

            // 行の要素は画面に見える数だけ作られ、スクロールで使い回される
            // Row elements exist only for visible rows and are recycled on scroll
            _list = new ListView(_visible, 32, () => CreateRow(), (row, index) => BindRow(context, row, _visible[index]));
            _list.AddToClassList("ov-vlist__list");
            Add(_list);
            _list.RegisterCallback<GeometryChangedEvent>(_ => SetStatus(0));
            schedule.Execute(ScrollStep).Every(0);
        }

        public void Filter(string query)
        {
            var start = Time.realtimeSinceStartupAsDouble;
            _visible.Clear();
            for (var i = 0; i < _all.Count; i++) if (query.Length == 0 || _all[i].name.Contains(query)) _visible.Add(i);
            _list.RefreshItems();
            SetStatus((Time.realtimeSinceStartupAsDouble - start) * 1000);
        }

        public void SetAutoScroll(bool enabled)
        {
            _autoScroll = enabled;
        }

        private void ScrollStep()
        {
            if (!_autoScroll) return;
            var scroller = _list.Q<ScrollView>().verticalScroller;
            scroller.value = scroller.value + 40f > scroller.highValue ? 0f : scroller.value + 40f;
        }

        private void SetStatus(double filterMilliseconds)
        {
            _status.text = $"{_visible.Count:N0} / {_all.Count:N0} 行　絞り込み {filterMilliseconds:0.0}ms　生成済みの行要素 {_list.Query(className: "ov-vlist__row").ToList().Count}";
        }

        private static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ov-vlist__row");
            var icon = new VisualElement { name = "icon" };
            icon.AddToClassList("ov-vlist__icon");
            row.Add(icon);
            row.Add(new Label { name = "name" });
            row.Add(new Label { name = "amount" });
            return row;
        }

        private void BindRow(UitkPocContext context, VisualElement row, int dataIndex)
        {
            var (name, amount) = _all[dataIndex];
            row.Q("icon").style.backgroundImage = context.LoadItemIcon(name.Substring(0, name.LastIndexOf(' ')));
            row.Q<Label>("name").text = name;
            row.Q<Label>("amount").text = amount.ToString("N0");
        }
    }
}
