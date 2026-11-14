using System;
using System.Collections.Generic;
using System.Globalization;
using Client.Localization;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 解放物1件を枠か文の行にする
    /// Turns one unlock into slots or text rows
    /// </summary>
    public static class ResearchUnlockEntryElements
    {
        // 解放物 1 件の要素を elements へ足し、載せる節を返す（種類→節と描画を 1 本の switch で持つ）
        // Adds one unlock's elements to elements and returns its section (kind-to-section and drawing share one switch)
        public static ResearchUnlockSectionKind AddTo(List<VisualElement> elements, UnlockEntry entry, GameContentPresentation content)
        {
            switch (entry)
            {
                case BlockUnlockEntry block:
                    var blockSlot = new BlockSlot($"research-unlock-block-{entry.Index}");
                    blockSlot.AddToClassList("research-tree__slot");
                    blockSlot.SetBlock(content.Block(block.Block.BlockId), UiText.FromContent(ContentLocalizationKeys.BlockName(block.Block.BlockGuid)));
                    elements.Add(blockSlot);
                    return ResearchUnlockSectionKind.Blocks;
                case MachineRecipeOutputUnlockEntry recipe:
                    // アイテムと液体は両方並べる
                    // Show both items and fluids
                    for (var i = 0; i < recipe.Recipe.OutputItemIds.Count; i++) elements.Add(ItemSlotOf($"research-unlock-recipe-{entry.Index}-{i}", recipe.Recipe.OutputItemIds[i], null, content));
                    foreach (var fluid in recipe.Recipe.OutputFluids) elements.Add(FluidLabelOf(fluid, content));
                    return ResearchUnlockSectionKind.MachineRecipes;
                case ItemRecipeViewUnlockEntry item:
                    elements.Add(ItemSlotOf($"research-unlock-item-{entry.Index}", item.ItemId, null, content));
                    return ResearchUnlockSectionKind.Items;
                case RewardItemUnlockEntry reward:
                    elements.Add(ItemSlotOf($"research-reward-item-{entry.Index}", reward.Reward.ItemId, reward.Reward.Count, content));
                    return ResearchUnlockSectionKind.RewardItems;
                case ConnectToolUnlockEntry tool:
                    elements.Add(OtherLine(null, UiText.FromContent(ContentLocalizationKeys.ConnectToolName(tool.Guid))));
                    return ResearchUnlockSectionKind.Others;
                case TrainCarUnlockEntry car:
                    elements.Add(OtherLine(null, UiText.FromContent(ContentLocalizationKeys.TrainCarName(car.Guid))));
                    return ResearchUnlockSectionKind.Others;
                case ModuleSlotStackLimitUnlockEntry limit:
                    elements.Add(OtherLine("research-unlock-module-slot-stack-limit", new UiText(LocalizationKeys.Ui.Research.UnlockModuleSlotStackLimit, new[] { new LocalizationTextParam("limit", limit.Limit.ToString(CultureInfo.InvariantCulture)) })));
                    return ResearchUnlockSectionKind.Others;
                case LevelInheritanceCraftCapUnlockEntry cap:
                    var capParams = new[] { new LocalizationTextParam("itemName", content.ItemName(cap.Cap.ItemId).Resolve()), new LocalizationTextParam("level", cap.Cap.Level.ToString(CultureInfo.InvariantCulture)) };
                    elements.Add(OtherLine($"research-unlock-craft-cap-{cap.Cap.ItemId.AsPrimitive()}", new UiText(LocalizationKeys.Ui.Research.UnlockLevelInheritanceCraftCap, capParams)));
                    return ResearchUnlockSectionKind.Others;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry.GetType().Name, null);
            }

            #region Internal

            // 液体は量の文だけで示す
            // Fluids show only an amount text
            LocalizedLabel FluidLabelOf(UnlockFluid fluid, GameContentPresentation content)
            {
                var label = new LocalizedLabel { name = "research-unlock-fluid" };
                label.AddToClassList("research-tree__unlock-fluid");
                var textParams = new[] { new LocalizationTextParam("fluidName", content.Fluid(fluid.FluidId).Label.Resolve()), new LocalizationTextParam("amount", SlotAmountFormat.Format(fluid.Amount)) };
                label.SetText(new UiText(LocalizationKeys.Ui.Research.UnlockFluidSummary, textParams));
                return label;
            }

            #endregion
        }

        private static ItemSlot ItemSlotOf(string frameTestid, ItemId itemId, int? count, GameContentPresentation content)
        {
            var slot = new ItemSlot(frameTestid);
            slot.AddToClassList("research-tree__slot");
            slot.SetContent(SlotItemContent.ForItem(content, itemId, count));
            return slot;
        }

        private static LocalizedLabel OtherLine(string testid, UiText text)
        {
            var label = new LocalizedLabel();
            if (testid != null) label.name = testid;
            label.AddToClassList("research-tree__unlock-other");
            label.SetText(text);
            return label;
        }
    }
}
