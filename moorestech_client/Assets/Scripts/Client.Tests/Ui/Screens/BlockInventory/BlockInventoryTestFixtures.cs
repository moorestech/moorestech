using Client.Localization;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.Commands;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

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
            var content = new GameContentPresentation(new FakeContentTextureSource(Texture, Texture, Texture));
            var grids = new BlockItemGridFactory(new RecordingBlockSlotCommands(), content, new UiCommandResultToaster(new GameUiToastFeed()));
            return new SectionStackBodyFactory(grids, new SectionStackSlotRegistry(plugged), source).Create(blockType, new BlockInventoryBodyMount(false));
        }
    }
}
