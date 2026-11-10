using System;
using System.Collections.Generic;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.Context;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.ModeHud;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の L1 の土台（品と水の名前を辞書へ重ね、偽の機械・文脈の Model と記録係のコマンドで区画を組む）
    /// Base of the machine-section L1 tests (overlays item and water names, builds the section with fake machine/context models and recording commands)
    /// </summary>
    public abstract class MachineScreenTestHost : BlockInventoryScreenTestHost
    {
        protected static readonly Guid FirstItemGuid = new("00000000-0000-0000-1234-000000000001");
        protected static readonly Guid SecondItemGuid = new("00000000-0000-0000-1234-000000000002");
        // 品と液体は別マスタなので同じ値でよい（テスト用 mod の水の GUID がこの値）
        // Items and fluids are separate masters, so sharing the value is fine (the test mod's water GUID is this)
        protected static readonly Guid WaterGuid = new("00000000-0000-0000-1234-000000000001");
        private static readonly Texture2D Texture = new(4, 4) { hideFlags = HideFlags.DontSave };

        private PlacementNameTestDictionary _names;

        protected FakeMachineSectionModel MachineModel { get; private set; }
        protected FakeBlockContextModel ContextModel { get; private set; }
        protected RecordingBlockSlotCommands Commands { get; private set; }
        protected RecordingRecipeSelectCommand RecipeSelect { get; private set; }
        protected FakeUiOsCursorApplier AppliedCursor { get; private set; }
        protected UiOsCursorRequestStack OsCursor { get; private set; }

        [SetUp]
        public void SetUpMachineScreen()
        {
            _names = new PlacementNameTestDictionary();
            _names.Merge($"{ContentLocalizationKeys.ItemName(FirstItemGuid).Key},Test Item 1,Test Item 1,テスト品1\n{ContentLocalizationKeys.ItemName(SecondItemGuid).Key},Test Item 2,Test Item 2,テスト品2\n{ContentLocalizationKeys.FluidName(WaterGuid).Key},Test Water,Test Water,テスト水\n");
            MachineModel = new FakeMachineSectionModel();
            ContextModel = new FakeBlockContextModel();
            Commands = new RecordingBlockSlotCommands();
            RecipeSelect = new RecordingRecipeSelectCommand();
            AppliedCursor = new FakeUiOsCursorApplier();
            OsCursor = new UiOsCursorRequestStack(AppliedCursor);
        }

        [TearDown]
        public void TearDownMachineScreen()
        {
            _names.Restore();
        }

        protected static ItemId FirstItem => MasterHolder.ItemMaster.GetItemId(FirstItemGuid);
        protected static ItemId SecondItem => MasterHolder.ItemMaster.GetItemId(SecondItemGuid);
        protected static FluidId Water => MasterHolder.FluidMaster.GetFluidId(WaterGuid);

        protected static GameContentPresentation Content => new(new FakeContentTextureSource(Texture, Texture, Texture));

        // View＋Presenter を組んで Attach し、機械の値を流してから載せる（Machine.uss は土台のテーマ GameUiTheme.tss から当たる）
        // Build the view and presenter, attach, push the machine value and mount (Machine.uss comes from the host theme GameUiTheme.tss)
        protected MachineSectionView MountSection(MachineSectionViewModel viewModel, bool fillsPanelHeight)
        {
            var view = new MachineSectionView(fillsPanelHeight, Content, Commands, Toaster, OsCursor, RecipeSelect);
            var presenter = new MachineSectionPresenter(view, MachineModel, ContextModel);
            presenter.Attach(MachineScreenViewModels.Panel());
            SetMachine(viewModel);
            Mount(view.Root);
            simulate.FrameUpdate();
            return view;
        }

        protected void SetMachine(MachineSectionViewModel viewModel)
        {
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(viewModel));
            simulate.FrameUpdate();
        }

        // 自分も祖先も隠されていない要素だけを数える
        // Count only elements hidden neither themselves nor via an ancestor
        protected static List<VisualElement> Shown(VisualElement root, string name)
        {
            var shown = new List<VisualElement>();
            foreach (var element in root.Query<VisualElement>(name).ToList())
            {
                if (IsEffectivelyShown(element)) shown.Add(element);
            }
            return shown;
        }

        protected static bool IsEffectivelyShown(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }
    }
}
