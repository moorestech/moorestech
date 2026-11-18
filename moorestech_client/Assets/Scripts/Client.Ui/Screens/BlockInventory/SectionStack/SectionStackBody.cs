using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の Presenter。固定順の区画を UiStack(Sm) に並べ、各区画の IsApplicable を購読して出し入れと間隔を組み直す（Web SectionStackView の写し）
    /// Standard-body presenter: fixed-order sections in a UiStack(Sm), subscribing each section's IsApplicable to re-lay visibility and gaps (port of Web SectionStackView)
    /// </summary>
    public sealed class SectionStackBody : IBlockInventoryPart
    {
        internal const string FillPanelHeightClassName = "section-stack--fill-panel-height";

        private readonly UiStack _stack = new(UiGap.Sm) { name = "section-stack" };
        private readonly IReadOnlyList<IBlockInventorySection> _sections;
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root { get; private set; }

        public SectionStackBody(IReadOnlyList<IBlockInventorySection> sections, bool fillsPanelHeight)
        {
            // 高さ確定の面でだけ伸ばす（高さ自動の面で伸ばすと子が内容高より縮む経路が開く）
            // Stretch only on the height-determining panel (on auto-height panels it would let children shrink below content)
            Root = _stack;
            _stack.EnableInClassList(FillPanelHeightClassName, fillsPanelHeight);
            _sections = sections;
            foreach (var section in sections)
            {
                _stack.AddItem(section.Root);
                _stack.SetItemShown(section.Root, false);
            }
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            foreach (var section in _sections)
            {
                section.Attach(panel);
                section.IsApplicable.Subscribe(shown => _stack.SetItemShown(section.Root, shown)).AddTo(_subscriptions);
            }
        }

        public void Detach()
        {
            _subscriptions.Clear();
            foreach (var section in _sections) section.Detach();
        }
    }
}
