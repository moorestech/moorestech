using System;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Components.Panels;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Routing;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// ブロックインベントリの画面 L1 の土台（テスト用 mod のマスタを読む。枠の絵は本物の Content が組む）
    /// Base of the block-inventory screen L1 tests (loads the test-mod master; slot icons come from the real Content)
    /// </summary>
    public abstract class BlockInventoryScreenTestHost : ScreenTestHost
    {
        [SetUp]
        public void SetUpBlockInventoryHost()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }

    // ブロックの枠の操作を受けるだけの記録係（この L1 は枠の中身ではなく並びを見る）
    // Command recorder that only receives block slot operations (this L1 checks layout, not slot contents)
    public sealed class RecordingBlockSlotCommands : CommandRecorder<string>, IBlockSlotCommands
    {
        public UiCommandResult LeftDown(int blockSlot, bool shift)
        {
            Record($"LeftDown:{blockSlot}:{shift}");
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult RightDown(int blockSlot)
        {
            Record($"RightDown:{blockSlot}");
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult DoubleClick(int blockSlot)
        {
            Record($"DoubleClick:{blockSlot}");
            return UiCommandAccepted.Instance;
        }
    }

    public static class BlockInventoryTestBodies
    {
        // DontSave の 1 枚を全テストで共有する（絵の中身は見ない）
        // One DontSave texture shared by all tests (the picture itself is never inspected)
        private static readonly Texture2D Texture = new(4, 4) { hideFlags = HideFlags.DontSave };

        // 標準の本文を本物の部品で組む（枠の操作は記録係、blockType は源のパネルから引くので引数は読みやすさのため）
        // Builds the standard body from real parts (slot commands go to a recorder; blockType is read from the source panel, so the argument is for readability)
        public static IBlockInventoryPart SectionStack(FakePanelSource source, string blockType, params ISectionStackSectionFactory[] plugged)
        {
            return new SectionStackBodyFactory(Grids(), new SectionStackSlotRegistry(plugged), source).Create(blockType, new BlockInventoryBodyMount(false));
        }

        // 本物の枠の並びの工場（枠の操作は記録係、絵は共有の 1 枚）
        // Real slot-grid factory (slot commands go to a recorder; icons share one texture)
        public static BlockItemGridFactory Grids()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(Texture, Texture, Texture));
            return new BlockItemGridFactory(new RecordingBlockSlotCommands(), content, new UiCommandResultToaster(new GameUiToastFeed()));
        }
    }

    /// <summary>
    /// 外殻の L1 の組み立て（偽の源・本物の LayoutModel・記録係の閉じる・本物のレジストリを 1280×720 の舞台の Viewer 列へ載せ、Presenter と同じ順で流す）
    /// Shell L1 assembly (fake source, real layout model, recording close and real registry on the 1280x720 stage Viewer column, fed in the presenter's order)
    /// </summary>
    public sealed class BlockInventoryTestShell
    {
        // 舞台の下の安全帯（alpha の --block-inventory-menu-lower-safe-area）
        // The stage's lower safe band (alpha's --block-inventory-menu-lower-safe-area)
        public const float MenuLowerSafeArea = 70f;
        private static readonly UiRoutingSnapshot SubInventory = new(UiScreen.SubInventory);

        public BlockInventoryView View { get; private set; }
        public RecordingPanelCommands Commands { get; private set; }
        public StageColumnsLayout Stage { get; private set; }

        private BlockInventoryTestShell(BlockInventoryView view, RecordingPanelCommands commands, StageColumnsLayout stage)
        {
            View = view;
            Commands = commands;
            Stage = stage;
        }

        public static BlockInventoryTestShell Create(ScreenTestHost host, ReactiveProperty<BlockInventoryPanelState> state, params object[] plugged)
        {
            // 寄与と工場は plugged から型で振り分ける
            // Rules and factories are sorted out of plugged by type
            var source = new FakePanelSource(state);
            var layout = new BlockInventoryPanelLayoutModel(source, plugged.OfType<IBlockInventoryLargePanelRule>().ToArray());
            layout.Initialize();
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("区画の登録が無い"));
            var sections = new SectionStackSlotRegistry(plugged.OfType<ISectionStackSectionFactory>().ToArray());
            var standard = new SectionStackBodyFactory(BlockInventoryTestBodies.Grids(), sections, source);
            var parts = new BlockInventoryPartRegistry(plugged.OfType<IBlockInventoryBodyFactory>().ToArray(), plugged.OfType<IBlockInventoryTitleActionFactory>().ToArray(), plugged.OfType<ITrainInventoryBodyFactory>().ToArray(), standard);

            var stage = new StageColumnsLayout();
            host.rootVisualElement.Add(stage);
            var commands = new RecordingPanelCommands();
            var view = new BlockInventoryView(stage, commands);
            var bodyHost = new BlockInventoryBodyHost(parts, view);
            stage.Get(StageColumn.Viewer).Add(view);

            // 出し分けは L0 の BlockInventoryScreenVisibility が持つので画面は SubInventory 固定。Presenter と同じ順で流す
            // Visibility is covered by the L0 BlockInventoryScreenVisibility, so the screen is fixed to SubInventory; feed in the presenter's order
            layout.Current.Subscribe(presentation =>
            {
                var shown = BlockInventoryScreenVisibility.IsShown(SubInventory, presentation.State);
                view.SetShown(shown);
                view.Render(presentation.State, presentation.Layout);
                bodyHost.Apply(shown ? presentation.State : BlockInventoryPanelState.Closed, presentation.Layout);
            });
            return new BlockInventoryTestShell(view, commands, stage);
        }

        public GamePanel Panel()
        {
            return View.Q<GamePanel>("block-inventory-panel");
        }

        public string TitleText()
        {
            return Panel().Q<Label>(className: "game-panel__title").text;
        }
    }

    // 閉じるの回数だけを数える記録係
    // Recorder that only counts close requests
    public sealed class RecordingPanelCommands : IBlockInventoryPanelCommands
    {
        public int CloseCount { get; private set; }

        public void Close()
        {
            CloseCount++;
        }
    }

    // 大型の寄与の偽物（値を変えたら Changed を流す）
    // Fake large-panel rule (fire Changed after changing the value)
    public sealed class FakeLargePanelRule : IBlockInventoryLargePanelRule
    {
        public readonly Subject<Unit> Changed = new();
        public bool Large;

        public FakeLargePanelRule(bool large)
        {
            Large = large;
        }

        public bool IsLargePanel(OpenBlockInventoryPanel panel)
        {
            return Large;
        }

        public IObservable<Unit> OnRuleInputChanged => Changed;
    }
}
