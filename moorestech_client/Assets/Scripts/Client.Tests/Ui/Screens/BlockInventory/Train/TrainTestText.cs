using System;
using Core.Master;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 本文のテストで文字と液体の表示を読む補助
    /// Helpers that read texts and fluid display in the body tests
    /// </summary>
    public static class TrainTestText
    {
        // テスト用 mod の水。液体の Guid の定数クラスは無いので前例（FluidMasterEndpointTest.TestWaterFluidGuid）と同じく直書きする
        // The test mod's water; there is no fluid Guid constants class, so it is written inline like the precedent FluidMasterEndpointTest.TestWaterFluidGuid
        public static readonly Guid WaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 要素とその子孫のうち見えている文字を空白でつなぐ（空の液体の行は文を消さず隠すので、隠れた文は数えない。Web は DOM から外す）
        // Joins the visible texts of an element and its descendants (an empty fluid row hides its texts instead of clearing them; Web removes them from the DOM)
        public static string TextsUnder(VisualElement root)
        {
            var texts = root.Query<TextElement>().ToList().FindAll(e => IsShownUnder(root, e));
            return string.Join(" ", texts.ConvertAll(e => e.text));
        }

        // 自分も root までの祖先も display:none でない
        // Neither the element nor its ancestors up to root are display:none
        private static bool IsShownUnder(VisualElement root, VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
                if (current == root) return true;
            }
            return true;
        }

        // 液体の表示名は本文と同じ引き方（GameContentPresentation の液体の UiIcon が持つ名前）
        // The fluid display name is read the same way as the body (the name carried by GameContentPresentation's fluid UiIcon)
        public static string FluidName(FluidId fluidId)
        {
            return BlockInventoryTestParts.Content().Fluid(fluidId).Label.Resolve();
        }

        // 枠にアイコンが出ているか（空の枠は GameIcon.ClearIcon で絵の状態クラスを外す。C4-a の FluidSlot.ShowEmpty）
        // Whether the slot shows an icon (an empty slot clears GameIcon's picture state class; C4-a FluidSlot.ShowEmpty)
        public static bool HasFluidIcon(VisualElement slotRow)
        {
            var icon = slotRow.Q(className: "fluid-slot__icon");
            return icon != null && icon.ClassListContains("game-icon--image");
        }
    }
}
