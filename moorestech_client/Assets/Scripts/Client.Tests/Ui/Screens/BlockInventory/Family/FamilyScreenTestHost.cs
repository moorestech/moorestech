using System;
using System.Collections.Generic;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.ModeHud;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 採掘機・ポンプ・発電機の L1 の土台
    /// Base of the miner, pump and generator L1 tests
    /// </summary>
    public abstract class FamilyScreenTestHost : BlockInventoryScreenTestHost
    {
        protected static readonly Guid FirstItemGuid = new("00000000-0000-0000-1234-000000000001");
        protected static readonly Guid SecondItemGuid = new("00000000-0000-0000-1234-000000000002");
        protected static readonly Guid WaterGuid = new("00000000-0000-0000-1234-000000000001");
        private static readonly Texture2D Texture = new(4, 4) { hideFlags = HideFlags.DontSave };

        private PlacementNameTestDictionary _names;

        protected FakeMinerDetailModel Miner { get; private set; }
        protected FakePumpDetailModel Pump { get; private set; }
        protected FakeGeneratorDetailModel Generator { get; private set; }

        [SetUp]
        public void SetUpFamilyNames()
        {
            _names = new PlacementNameTestDictionary();
            _names.Merge($"{ContentLocalizationKeys.ItemName(FirstItemGuid).Key},Test Item 1,Test Item 1,テスト品1\n{ContentLocalizationKeys.ItemName(SecondItemGuid).Key},Test Item 2,Test Item 2,テスト品2\n{ContentLocalizationKeys.FluidName(WaterGuid).Key},Test Water,Test Water,テスト水\n");
            Miner = new FakeMinerDetailModel();
            Pump = new FakePumpDetailModel();
            Generator = new FakeGeneratorDetailModel();
        }

        [TearDown]
        public void TearDownFamilyNames()
        {
            _names.Restore();
        }

        protected static ItemId FirstItem => MasterHolder.ItemMaster.GetItemId(FirstItemGuid);
        protected static ItemId SecondItem => MasterHolder.ItemMaster.GetItemId(SecondItemGuid);
        protected static FluidId Water => MasterHolder.FluidMaster.GetFluidId(WaterGuid);

        protected static GameContentPresentation Content => new(new FakeContentTextureSource(Texture, Texture, Texture));

        // 本物の 3 工場（採掘機・ポンプ・発電機の順）
        // The three real factories (miner, pump, generator)
        protected IBlockInventoryPart MountBody()
        {
            var panel = BlockInventoryTestPanels.OfType("CleanRoomAirFilter");
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel));
            var factories = new Client.Ui.Screens.BlockInventory.SectionStack.ISectionStackSectionFactory[]
            {
                new MinerSectionFactory(Content, Miner), new PumpSectionFactory(Content, Pump), new GeneratorSectionFactory(Generator),
            };
            var body = BlockInventoryTestBodies.SectionStack(source, "CleanRoomAirFilter", factories);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)panel);
            simulate.FrameUpdate();
            return body;
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

        protected static List<VisualElement> Shown(VisualElement root, string name)
        {
            var shown = new List<VisualElement>();
            foreach (var element in root.Query<VisualElement>(name).ToList())
            {
                if (IsEffectivelyShown(element)) shown.Add(element);
            }
            return shown;
        }
    }
}
