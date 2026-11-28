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
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

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
        protected BlockInventoryTestShell OpenAirFilterPanel()
        {
            ContextModel.Set(AirFilterRoom());
            var panel = BlockInventoryTestPanels.OfType("CleanRoomAirFilter", S.CleanRoomAirFilter, new SlotItem(FirstItem, 5));
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

        // 部屋の中の大型機械。inRoom が偽なら部屋なし
        // The large interior machine; no room when inRoom is false
        protected BlockInventoryTestShell OpenCleanRoomMachine(bool inRoom)
        {
            var shell = OpenCleanRoomMachine(inRoom, new MachineSlotLayout(2, 1, 1), true);
            Assert.IsTrue(shell.View.ClassListContains(BlockInventoryView.LargeClassName), "machine panel is large");
            return shell;
        }

        // 枠数とレシピ有無を指定する室内機械
        // The interior machine with the given slot layout and recipe presence
        protected BlockInventoryTestShell OpenCleanRoomMachine(bool inRoom, MachineSlotLayout layout, bool withRecipes)
        {
            var slots = Enumerable.Repeat(new SlotItem(new ItemId(0), 0), layout.Input + layout.Output + layout.Module).ToArray();
            var selection = withRecipes ? MachineRecipeSelection.Selected(MachineTestDetails.RecipeA) : MachineRecipeSelection.Unselected;
            var detail = MachineTestDetails.Detail(selection, MachineTestDetails.Unbound(layout), 0.42f);
            var fluids = new[] { BlockFluidSlotState.Filled(Water, 50, 100), BlockFluidSlotState.Empty(100) };
            var panel = MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, slots);
            var catalog = withRecipes ? MachineTestDetails.Catalog(MachineTestDetails.RecipeA, MachineTestDetails.RecipeB) : MachineRecipeCatalog.Empty;
            var viewModel = MachineSectionViewModelBuilder.Build(panel, detail, slots, fluids, catalog, 1);
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(viewModel));
            if (inRoom) ContextModel.Set(MachineRoom());
            return OpenShell(panel);
        }

        private BlockInventoryTestShell OpenShell(BlockInventoryPanelState panel)
        {
            // 本番と同じく機械・文脈の工場と寄与を差す
            // Plug machine, context factories and rule, as production
            var rule = new RecipeMachineLargePanelRule(MachineModel);
            rule.Initialize();
            var machine = new MachineSectionFactory(MachineModel, ContextModel, Commands, Content, new RecordingRecipeSelectCommand(), OsCursor, ImprovementCommands);
            var context = new ContextSectionFactory(Content, ContextModel, MachineModel, OsCursor, ImprovementCommands);
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(panel), rule, machine, context);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return shell;
        }

        // 室内機械から見た密閉済みの部屋（部屋クラスの1行）
        // A sealed room as an interior machine sees it (the room-class line)
        public static BlockContextViewModel MachineRoom()
        {
            return Room(CleanRoomSummary.ForMachine(CleanRoomHeadline.Class("C")));
        }

        // 清浄機から見た密閉済みの部屋（台数つきの見出し・台数・この清浄機の3行）
        // A sealed room as a purifier sees it (headline with units, count and own status)
        public static BlockContextViewModel AirFilterRoom()
        {
            var status = CleanRoomAirFilterRoomStatus.WithUnitsToNextClass(6, 1, 1, 0.5, "B", 5);
            return Room(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.Class("C"), status));
        }

        private static BlockContextViewModel Room(CleanRoomSummary room)
        {
            var entry = SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(room, CleanRoomHaltReason.None));
            return new BlockContextViewModel(entry, SectionContent<CleanRoomYieldOdds>.Absent, SectionContent<MachineInputStall>.Absent);
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
