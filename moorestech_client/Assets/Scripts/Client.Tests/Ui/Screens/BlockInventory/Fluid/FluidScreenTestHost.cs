using System;
using System.Collections.Generic;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.ModeHud;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の区画の L1 の土台
    /// Base of the fluid-section L1 tests
    /// </summary>
    public abstract class FluidScreenTestHost : BlockInventoryScreenTestHost
    {
        protected static readonly Guid WaterGuid = new("00000000-0000-0000-1234-000000000001");
        protected static readonly Guid TankGuid = new("00000000-0000-0000-0000-0000000f1a4c");
        private static readonly Texture2D Texture = new(4, 4) { hideFlags = HideFlags.DontSave };

        private PlacementNameTestDictionary _names;

        protected FakeBlockFluidSlotsModel Fluids { get; private set; }
        protected FakeMachineSectionModel Machine { get; private set; }

        [SetUp]
        public void SetUpFluidNames()
        {
            _names = new PlacementNameTestDictionary();
            _names.Merge($"{ContentLocalizationKeys.FluidName(WaterGuid).Key},Test Water,Test Water,テスト水\n{ContentLocalizationKeys.BlockName(TankGuid).Key},Fluid Tank,Fluid Tank,タンク\n");
            Fluids = new FakeBlockFluidSlotsModel();
            Machine = new FakeMachineSectionModel();
        }

        [TearDown]
        public void TearDownFluidNames()
        {
            _names.Restore();
        }

        protected static FluidId Water => MasterHolder.FluidMaster.GetFluidId(WaterGuid);

        protected static GameContentPresentation ContentWith(Texture2D fluidTexture)
        {
            return new GameContentPresentation(new FakeContentTextureSource(Texture, Texture, fluidTexture));
        }

        // 本物の 2 工場（共有の 1 枚を絵にする）
        // The two real factories (all pictures share one texture)
        protected ISectionStackSectionFactory[] Factories(GameContentPresentation content)
        {
            return new ISectionStackSectionFactory[] { new FluidRowSectionFactory(content, Fluids, Machine), new FluidDetailsSectionFactory(content, Fluids) };
        }

        protected IBlockInventoryPart MountBody(string blockType)
        {
            var panel = BlockInventoryTestPanels.OfType(blockType);
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel));
            var body = BlockInventoryTestBodies.SectionStack(source, blockType, Factories(ContentWith(Texture)));
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)panel);
            simulate.FrameUpdate();
            return body;
        }

        protected void SetFluids(float? progress, params BlockFluidSlotState[] slots)
        {
            Fluids.Set(new BlockFluidSlotsViewModel(slots, progress.HasValue, progress ?? 0f));
            simulate.FrameUpdate();
        }

        // 自分も祖先も隠れていない要素を数える
        // Count elements hidden neither themselves nor via an ancestor
        protected static bool IsEffectivelyShown(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }

        protected static List<T> Shown<T>(VisualElement root, string name, string className) where T : VisualElement
        {
            var shown = new List<T>();
            foreach (var element in root.Query<T>(name, className).ToList())
            {
                if (IsEffectivelyShown(element)) shown.Add(element);
            }
            return shown;
        }
    }
}
