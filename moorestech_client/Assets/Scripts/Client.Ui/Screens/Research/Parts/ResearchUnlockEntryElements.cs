using System;
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
        public static void AddTo(VisualElement container, UnlockEntry entry, GameContentPresentation content)
        {
            switch (entry)
            {
                case BlockUnlockEntry block:
                    var blockSlot = new BlockSlot($"research-unlock-block-{entry.Index}");
                    blockSlot.AddToClassList("research-tree__slot");
                    blockSlot.SetBlock(content.Block(block.Block.BlockId), UiText.FromContent(ContentLocalizationKeys.BlockName(block.Block.BlockGuid)));
                    container.Add(blockSlot);
                    break;
                case MachineRecipeOutputUnlockEntry recipe:
                    // アイテムと液体は両方並べる
                    // Show both items and fluids
                    for (var i = 0; i < recipe.Recipe.OutputItemIds.Count; i++) container.Add(ItemSlotOf($"research-unlock-recipe-{entry.Index}-{i}", recipe.Recipe.OutputItemIds[i], null, content));
                    foreach (var fluid in recipe.Recipe.OutputFluids) container.Add(FluidLabelOf(fluid, content));
                    break;
                case ItemRecipeViewUnlockEntry item:
                    container.Add(ItemSlotOf($"research-unlock-item-{entry.Index}", item.ItemId, null, content));
                    break;
                case RewardItemUnlockEntry reward:
                    container.Add(ItemSlotOf($"research-reward-item-{entry.Index}", reward.Reward.ItemId, reward.Reward.Count, content));
                    break;
                case ConnectToolUnlockEntry tool:
                    container.Add(OtherLine(null, UiText.FromContent(ContentLocalizationKeys.ConnectToolName(tool.Guid))));
                    break;
                case TrainCarUnlockEntry car:
                    container.Add(OtherLine(null, UiText.FromContent(ContentLocalizationKeys.TrainCarName(car.Guid))));
                    break;
                case ModuleSlotStackLimitUnlockEntry limit:
                    container.Add(OtherLine("research-unlock-module-slot-stack-limit", new UiText(LocalizationKeys.Ui.Research.UnlockModuleSlotStackLimit, new[] { new LocalizationTextParam("limit", limit.Limit.ToString(CultureInfo.InvariantCulture)) })));
                    break;
                case LevelInheritanceCraftCapUnlockEntry cap:
                    var capParams = new[] { new LocalizationTextParam("itemName", content.ItemName(cap.Cap.ItemId).Resolve()), new LocalizationTextParam("level", cap.Cap.Level.ToString(CultureInfo.InvariantCulture)) };
                    container.Add(OtherLine($"research-unlock-craft-cap-{cap.Cap.ItemId.AsPrimitive()}", new UiText(LocalizationKeys.Ui.Research.UnlockLevelInheritanceCraftCap, capParams)));
                    break;
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
            slot.SetContent(new ItemSlotContent(content.Item(itemId), count, new ItemSlotMarks(false, false, false), null, null, null));
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
