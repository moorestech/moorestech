using System;
using System.Collections.Generic;
using System.Globalization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using Core.Master;
using Game.Research;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 詳細ペインの解放物の節（L1）
    /// Detail pane unlock sections (L1)
    /// </summary>
    public class ResearchUnlockSectionsTest : ScreenTestHost
    {
        private Texture2D _texture;
        private GameContentPresentation _content;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究報酬itemの個数をtopic payloadどおり詳細ペインで表示する</web-test>
        [Test]
        public void 報酬アイテムはマスタの個数つきで報酬の節に並ぶ()
        {
            var unlocks = Kit.MasterUnlocks(Kit.RewardResearch);
            MountPane(unlocks);

            var section = Find<VisualElement>(rootVisualElement, "research-reward-items");
            Assert.AreEqual("Reward items", SectionLabel(section));
            var count = unlocks.RewardItems[0].Count.ToString(CultureInfo.InvariantCulture);
            Assert.IsTrue(section.Query<Label>().ToList().Exists(label => label.PlainText() == count), $"count {count}");
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">詳細ペインに解放物が種類別ラベル付きで並ぶ</web-test>
        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">解放ブロックがあればBlockSlotで描画される</web-test>
        [Test]
        public void 解放物は種類ごとの見出しの節に初出順で並びブロックはブロックの枠で描く()
        {
            MountPane(Kit.MasterUnlocks(Kit.BlockAndToolsResearch));

            var blocks = Find<VisualElement>(rootVisualElement, "research-unlock-blocks");
            var recipes = Find<VisualElement>(rootVisualElement, "research-unlock-machine-recipes");
            var others = Find<VisualElement>(rootVisualElement, "research-unlock-others");
            Assert.AreEqual("Unlocks: Blocks", SectionLabel(blocks));
            Assert.AreEqual("Unlocks: Machine recipes", SectionLabel(recipes));
            Assert.AreEqual("Unlocks: Others", SectionLabel(others));
            Assert.Less(blocks.parent.IndexOf(blocks), recipes.parent.IndexOf(recipes));
            Assert.IsNotNull(blocks.Q<BlockSlot>());
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">機械レシピはアイテム出力と液体出力を両方描く（混在レシピの液体を落とさない）</web-test>
        [Test]
        public void 機械レシピはアイテムの枠と液体の量の行を両方描く()
        {
            var water = MasterHolder.FluidMaster.GetFluidId(Kit.WaterFluid);
            var recipe = new UnlockMachineRecipe(Guid.NewGuid(), new List<ItemId> { ForUnitTestItemId.ItemId1 }, new List<UnlockFluid> { new(water, 250d) });
            MountPane(Kit.Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe> { recipe }, new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int>(), new List<LevelInheritanceCraftCap>()));

            var section = Find<VisualElement>(rootVisualElement, "research-unlock-machine-recipes");
            Assert.AreEqual(1, section.Query<ItemSlot>().ToList().Count);
            var fluid = Find<Label>(section, "research-unlock-fluid");
            // 液体名は部品と同じ解決で比べる
            // Compare fluid names by the part's resolution
            StringAssert.Contains(_content.Fluid(water).Label.Resolve(), fluid.text);
            StringAssert.Contains(SlotAmountFormat.Format(250d), fluid.text);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">itemRecipeView解放とconnectTool/trainCarのテキスト行が並ぶ</web-test>
        [Test]
        public void レシピ表示の解放は枠で接続ツールと車両はその他の文の行で並ぶ()
        {
            var master = Kit.MasterUnlocks(Kit.BlockAndToolsResearch);
            MountPane(Kit.Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId> { ForUnitTestItemId.ItemId1 }, new List<RewardItem>(), master.ConnectToolGuids, master.TrainCarGuids, new List<int>(), new List<LevelInheritanceCraftCap>()));

            Assert.IsNotNull(Find<VisualElement>(rootVisualElement, "research-unlock-items").Q<ItemSlot>());
            var lines = Find<VisualElement>(rootVisualElement, "research-unlock-others").Query<Label>(className: "research-tree__unlock-other").ToList();
            Assert.AreEqual(master.ConnectToolGuids.Count + master.TrainCarGuids.Count, lines.Count);
            Assert.AreEqual(UiText.FromContent(ContentLocalizationKeys.ConnectToolName(master.ConnectToolGuids[0])).Resolve(), lines[0].PlainText());
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">スロット上限と製作上限の解放を「その他」の行に出す</web-test>
        /// <web-test file="e2e/tests/research/researchMachineUpgradeUnlock.spec.ts">研究ノードの詳細にスロット上限と製作上限の解放行が出る</web-test>
        [Test]
        public void モジュール枠の上限と製作上限はその他の文の行に出る()
        {
            var limit = Kit.MasterUnlocks(Kit.ModuleSlotResearch).ModuleSlotStackLimits[0];
            var cap = Kit.MasterUnlocks(Kit.CraftCapResearch).LevelInheritanceCraftCaps[0];
            MountPane(Kit.Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int> { limit }, new List<LevelInheritanceCraftCap> { cap }));

            Assert.AreEqual($"Module slot limit: {limit} per slot", Find<Label>(rootVisualElement, "research-unlock-module-slot-stack-limit").PlainText());
            var capLine = Find<Label>(rootVisualElement, $"research-unlock-craft-cap-{cap.ItemId.AsPrimitive()}");
            StringAssert.EndsWith($"up to Lv{cap.Level}", capLine.PlainText());
        }

        #region Helpers

        private void MountPane(ResearchUnlockSet unlocks)
        {
            var pane = new ResearchDetailPane(new RecordingResearchTreeCommands(), _content, Toaster);
            pane.Render(Kit.Detail(ResearchNodeState.Researchable, 3, 0, unlocks));
            Mount(pane);
            simulate.FrameUpdate();
        }

        private static string SectionLabel(VisualElement section)
        {
            return section.Q<Label>(className: "research-tree__section-label").PlainText();
        }

        #endregion
    }
}
