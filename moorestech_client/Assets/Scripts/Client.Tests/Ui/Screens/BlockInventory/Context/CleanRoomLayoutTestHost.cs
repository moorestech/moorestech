using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Machine;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 清浄室の配置の写しの土台
    /// Base of the clean-room layout ports
    /// </summary>
    public abstract class CleanRoomLayoutTestHost : MachineScreenTestHost
    {
        // Web の最小視口高と同じ
        // Same floor as Web MIN_BODY_VIEWPORT_HEIGHT, catching a collapsed viewport
        protected const float MinBodyViewportHeight = 120f;
        private const float Tolerance = 0.5f;

        // 清浄機の面。大型でないことを先に確かめる
        // The purifier panel; asserts non-large first like Web openBlock
        protected BlockInventoryTestShell OpenAirFilterPanel(int filterCount)
        {
            ContextModel.Set(Room(filterCount));
            var panel = BlockInventoryTestPanels.OfType("CleanRoomAirFilter", new SlotItem(FirstItem, 5));
            var shell = OpenShell(panel);
            Assert.IsFalse(shell.View.ClassListContains(BlockInventoryView.LargeClassName), "air filter panel is not large");
            return shell;
        }

        // Web の setViewportSize の写し: 舞台を縮める（720 の土台の内側だけ。BlockInventoryPanelBandTest の前例）
        // Port of the Web setViewportSize: shrink the stage (only within the 720 host; BlockInventoryPanelBandTest precedent)
        protected void SetStageHeight(BlockInventoryTestShell shell, float stageHeight)
        {
            shell.Stage.style.height = stageHeight;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        // 部屋の中の大型機械。負なら部屋なし
        // The large interior machine; negative filterCount means no room
        protected BlockInventoryTestShell OpenCleanRoomMachine(int filterCount)
        {
            var slots = Enumerable.Repeat(new SlotItem(new ItemId(0), 0), 4).ToArray();
            var detail = MachineTestDetails.Detail(MachineRecipeSelection.Selected(MachineTestDetails.RecipeA), MachineTestDetails.Unbound(new MachineSlotLayout(2, 1, 1)), 0.42f);
            var fluids = new[] { BlockFluidSlotState.Filled(Water, 50, 100), BlockFluidSlotState.Empty(100) };
            var panel = MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, slots);
            var viewModel = MachineSectionViewModelBuilder.Build(panel, detail, slots, fluids, MachineTestDetails.Catalog(MachineTestDetails.RecipeA, MachineTestDetails.RecipeB), 1);
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(viewModel));
            if (0 <= filterCount) ContextModel.Set(Room(filterCount));
            var shell = OpenShell(panel);
            Assert.IsTrue(shell.View.ClassListContains(BlockInventoryView.LargeClassName), "machine panel is large");
            return shell;
        }

        private BlockInventoryTestShell OpenShell(BlockInventoryPanelState panel)
        {
            // 本番と同じく機械・文脈の工場と寄与を差す
            // Plug machine, context factories and rule, as production
            var rule = new RecipeMachineLargePanelRule(MachineModel);
            rule.Initialize();
            var machine = new MachineSectionFactory(MachineModel, ContextModel, Commands, Toaster, Content, new RecordingRecipeSelectCommand(), OsCursor);
            var context = new ContextSectionFactory(Content, ContextModel, MachineModel);
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(panel), rule, machine, context);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return shell;
        }

        // filterCount 台の密閉済みの部屋
        // A sealed room with filterCount purifiers
        public static BlockContextViewModel Room(int filterCount)
        {
            var filters = Enumerable.Range(0, filterCount).Select(_ => new CleanRoomAirFilter(1, 5, 0.5)).ToArray();
            var room = new CleanRoomSummary(CleanRoomHeadline.Class("ISO 5"), 1.2, 0.8, filters);
            var entry = SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(room, CleanRoomHaltReason.None));
            return new BlockContextViewModel(entry, SectionContent<CleanRoomYieldFetched>.Absent, SectionContent<MachineInputStall>.Absent);
        }

        // Web expectReachableInScroll
        // Web expectReachableInScrollViewport: edges inside the viewport, hit at centre
        protected void ExpectReachableInScrollViewport(GameScrollView scroll, VisualElement target)
        {
            scroll.ScrollTo(target);
            simulate.FrameUpdate();
            var box = target.worldBound;
            var viewport = scroll.contentViewport.worldBound;
            Assert.GreaterOrEqual(box.yMin, viewport.yMin - Tolerance, $"{target.name} top");
            Assert.LessOrEqual(box.yMax, viewport.yMax + Tolerance, $"{target.name} bottom");
            ExpectHitAtCenter(target);
        }

        protected void ExpectReachable(BlockInventoryTestShell shell, string name)
        {
            var target = shell.View.Q(name);
            ExpectReachableInScrollViewport(target.GetFirstAncestorOfType<GameScrollView>(), target);
        }

        // Web expectFooterReadable の写し
        // Web expectFooterReadable: the footer is hit at its centre
        protected static void ExpectFooterReadable(BlockInventoryTestShell shell)
        {
            var panel = shell.View.worldBound;
            foreach (var name in new[] { "machine-state-label", "machine-power-rate" })
            {
                var footer = shell.View.Q(name);
                Assert.IsNull(footer.GetFirstAncestorOfType<GameScrollView>(), $"{name} scroller");
                Assert.GreaterOrEqual(footer.worldBound.yMin, panel.yMin - Tolerance, $"{name} top");
                Assert.LessOrEqual(footer.worldBound.yMax, panel.yMax + Tolerance, $"{name} bottom");
                ExpectHitAtCenter(footer);
            }
        }

        // Web expectSharedBody の写し
        // Web expectSharedBodyViewport: room and body share one viewport ending above the footer
        protected static void ExpectSharedBodyViewport(BlockInventoryTestShell shell, string bodyName)
        {
            var scroll = shell.View.Q(bodyName).GetFirstAncestorOfType<GameScrollView>();
            var viewport = scroll.contentViewport.worldBound;
            Assert.GreaterOrEqual(viewport.height, MinBodyViewportHeight, "body viewport height");
            Assert.LessOrEqual(viewport.yMax, shell.View.Q("machine-state-label").worldBound.yMin + Tolerance, "viewport above footer");
            Assert.IsNotNull(scroll.contentContainer.Q("clean-room-section"), "room inside the body scroller");
            Assert.IsNull(scroll.GetFirstAncestorOfType<GameScrollView>(), "nested scroller");
        }

        protected void ExpectInventoryBodyReachable(BlockInventoryTestShell shell)
        {
            ExpectFooterReadable(shell);
            foreach (var name in new[] { "machine-selected-recipe", "machine-input-slots", "machine-output-slots", "machine-fluid-slots" }) ExpectReachable(shell, name);
            ExpectSharedBodyViewport(shell, "machine-inventory-body");
        }

        // Web expectHitTestWithin の写し
        // As Web expectHitTestWithin, pickable only while measuring; the centre Pick is the target or a descendant
        private static void ExpectHitAtCenter(VisualElement target)
        {
            var original = target.pickingMode;
            target.pickingMode = PickingMode.Position;
            var picked = target.panel.Pick(target.worldBound.center);
            target.pickingMode = original;
            Assert.IsTrue(picked != null && (picked == target || target.Contains(picked)), $"{target.name} hit by {picked?.name}");
        }
    }
}
