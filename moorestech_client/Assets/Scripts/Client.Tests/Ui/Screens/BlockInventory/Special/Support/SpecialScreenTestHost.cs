using System;
using System.Collections.Generic;
using System.Linq;
using Client.Input.ArrowKeys;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mod.Loader;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// 特殊族の L1 の土台。偽の Model・記録する操作・矢印キー・表示素材と、ゲージ・棒・文字の読み取りを持つ
    /// L1 base for the special family: fake model, recording commands, arrow keys and content, plus readers for gauges, bars and text
    /// </summary>
    public abstract class SpecialScreenTestHost : ScreenTestHost
    {
        protected static readonly IReadOnlyList<SlotItem> NoSlots = Array.Empty<SlotItem>();

        protected FakeSpecialBlockDetailModel Model { get; private set; }
        protected RecordingSpecialBlockCommands Commands { get; private set; }
        protected FakeArrowKeys ArrowKeys { get; private set; }
        protected FakePanelSource Panels { get; private set; }
        protected SpecialPresenters Presenters { get; private set; }

        [SetUp]
        public void SetUpSpecialHost()
        {
            Model = new FakeSpecialBlockDetailModel();
            Commands = new RecordingSpecialBlockCommands();
            ArrowKeys = new FakeArrowKeys();
            Panels = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed));
            Presenters = new SpecialPresenters(Model, Panels, ArrowKeys);
        }

        // alpha の土台でパネルを開いた状態にして返す（Presenter の Attach に渡す）
        // Opens a panel with alpha's fixtures and returns it (passed to a presenter's Attach)
        protected OpenBlockInventoryPanel OpenPanel(string blockType, params SlotItem[] slots)
        {
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(blockType, slots);
            Panels.State.Value = panel;
            return panel;
        }

        // アイテムの名前・絵が要るテストだけがテスト用 mod のマスタを読む（前例 GameContentPresentationTest）
        // Only tests that need item names and pictures load the test mod master (precedent GameContentPresentationTest)
        protected static GameContentPresentation LoadContent()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new GameContentPresentation(new FakeContentTextureSource(null, null, null));
        }

        protected static string TextOf(VisualElement root, string testid)
        {
            return Find<Label>(root, testid).text;
        }

        protected static string Resolved(UiText text)
        {
            return text.Resolve();
        }

        // ゲージの充填の割合（.gauge-bar__fill の width の %）
        // Gauge fill ratio from the width percent of .gauge-bar__fill
        protected static float GaugeValue(VisualElement root, string testid)
        {
            return Find<VisualElement>(root, testid).Q(className: "gauge-bar__fill").style.width.value.value / 100f;
        }

        // 棒の値（空の棒は null、他は充填の高さの割合）
        // Bar values: null for an empty bar, otherwise the fill height ratio
        protected static float?[] BarValues(VisualElement root, string testid)
        {
            return Find<VisualElement>(root, testid).Query(className: "bar-series__bar").ToList()
                .Select(bar => bar.ClassListContains("bar-series__bar--empty") ? (float?)null : bar.Q(className: "bar-series__bar-fill").style.height.value.value / 100f)
                .ToArray();
        }

        protected sealed class FakeArrowKeys : IHorizontalArrowKeySource
        {
            private readonly Subject<HorizontalArrowKey> _pressed = new();

            public IObservable<HorizontalArrowKey> OnArrowPressed => _pressed;

            public void Press(HorizontalArrowKey key)
            {
                _pressed.OnNext(key);
            }
        }
    }
}
