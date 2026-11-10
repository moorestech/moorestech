using System;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.ModeHud;
using Client.Ui.Content;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の 3 欄の L1 の土台（テスト用の品の名前を辞書へ重ね、表示中の子の名前を数える）
    /// Base of the context-section L1 tests (overlays a test item name and lists the names of shown children)
    /// </summary>
    public abstract class ContextSectionTestHost : BlockInventoryScreenTestHost
    {
        protected static readonly Guid PlateGuid = new("00000000-0000-0000-1234-000000000001");
        private static readonly Texture2D Texture = new(4, 4) { hideFlags = HideFlags.DontSave };

        private PlacementNameTestDictionary _names;

        [SetUp]
        public void SetUpContextNames()
        {
            _names = new PlacementNameTestDictionary();
            _names.Merge($"{ContentLocalizationKeys.ItemName(PlateGuid).Key},Test Plate,Test Plate,テスト鉄板\n");
        }

        [TearDown]
        public void TearDownContextNames()
        {
            _names.Restore();
        }

        protected static GameContentPresentation Content => new(new FakeContentTextureSource(Texture, Texture, Texture));

        protected static ItemId PlateId => MasterHolder.ItemMaster.GetItemId(PlateGuid);

        // 表示中（自分が隠されていない）の直下の子の名前
        // Names of direct children that are not hidden themselves
        protected static string[] ShownNames(VisualElement parent)
        {
            return parent.Children().Where(e => e.resolvedStyle.display == DisplayStyle.Flex).Select(e => e.name).ToArray();
        }
    }
}
