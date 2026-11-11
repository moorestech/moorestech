using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Hotbar;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Routing;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの ViewModel の組み立て（選択・受理・表示・未解決。L0）
    /// Building the hotbar view model (selection, acceptance, visibility, unresolved; L0)
    /// </summary>
    public class HotbarViewModelFactoryTest
    {
        private static readonly HotbarSlotViewModel[] NineEmpty = Enumerable.Repeat<HotbarSlotViewModel>(HotbarEmptySlot.Instance, 9).ToArray();

        [Test]
        public void 由来がホットバーの枠ならその枠を選択しそれ以外は選択なし()
        {
            var fromSlot = HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.FromHotbarSlot(2), UIStateEnum.GameScreen, UiScreen.None, false);
            Assert.IsTrue(fromSlot.Selection.IsSlot(2));
            Assert.IsFalse(fromSlot.Selection.IsSlot(0));

            var fromMenu = HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.NonHotbar, UIStateEnum.PlaceBlock, UiScreen.None, false);
            Assert.IsFalse(fromMenu.Selection.TryGetSlot(out _));
        }

        [Test]
        public void 選択を受けるのはゲーム画面と建築モードだけ()
        {
            // Web uiStateAcceptsHotbarSelect と HotbarSelectActionHandler の 2 状態
            // The two states of Web uiStateAcceptsHotbarSelect and HotbarSelectActionHandler
            foreach (UIStateEnum state in Enum.GetValues(typeof(UIStateEnum)))
            {
                var expected = state == UIStateEnum.GameScreen || state == UIStateEnum.PlaceBlock;
                Assert.AreEqual(expected, HotbarSelectRule.Accepts(state), state.ToString());
                Assert.AreEqual(expected, HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.NonHotbar, state, UiScreen.None, false).SelectAccepted, state.ToString());
            }
        }

        [Test]
        public void 常駐HUDを出す画面で見えて研究画面とblockingスキットで退く()
        {
            Assert.IsTrue(Create(UiScreen.None, false).Visible);
            Assert.IsTrue(Create(UiScreen.PlayerInventory, false).Visible);
            Assert.IsTrue(Create(UiScreen.BuildMenu, false).Visible);
            Assert.IsFalse(Create(UiScreen.ResearchTree, false).Visible);
            Assert.IsFalse(Create(UiScreen.None, true).Visible);

            #region Internal

            HotbarViewModel Create(UiScreen screen, bool blockingSkit)
            {
                return HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.NonHotbar, UIStateEnum.GameScreen, screen, blockingSkit);
            }

            #endregion
        }

        [Test]
        public void 同じ中身なら等しく絵の出どころか表示名が変われば等しくない()
        {
            // BP のサムネイルの到着と改名は ViewModel の値の違いとして届き、同じ値なら描き直さない
            // Blueprint thumbnail arrival and renames arrive as view-model value changes; equal values skip the redraw
            var targetId = Guid.NewGuid();
            var nameOnly = Assigned(new HotbarAssignedSlot(targetId, "参照拠点", PlacementTargetNameOnlyIcon.Instance));
            var sameNameOnly = Assigned(new HotbarAssignedSlot(targetId, "参照拠点", PlacementTargetNameOnlyIcon.Instance));
            var captured = Assigned(new HotbarAssignedSlot(targetId, "参照拠点", new PlacementTargetBlueprintIcon(targetId)));
            var renamed = Assigned(new HotbarAssignedSlot(targetId, "第二拠点", PlacementTargetNameOnlyIcon.Instance));
            Assert.AreEqual(nameOnly, sameNameOnly);
            Assert.AreNotEqual(nameOnly, captured);
            // BP の絵は参照の等価なので、同じ BP でも組み直せば等しくない（撮り直しを描き直す前提。計画 D-19）
            // A blueprint icon has reference equality, so the same blueprint rebuilt is unequal (the premise for redrawing a recapture; plan D-19)
            Assert.AreNotEqual(captured, Assigned(new HotbarAssignedSlot(targetId, "参照拠点", new PlacementTargetBlueprintIcon(targetId))));
            Assert.AreNotEqual(nameOnly, renamed);
            Assert.IsTrue(nameOnly.Slots[4].IsAssigned);
            Assert.IsFalse(nameOnly.Slots[0].IsAssigned);

            // 未解決の枠も割当あり（外せる）として扱う
            // An unresolved slot also counts as assigned (clearable)
            Assert.IsTrue(new HotbarUnresolvedSlot(targetId).IsAssigned);

            #region Internal

            HotbarViewModel Assigned(HotbarSlotViewModel slot4)
            {
                var slots = NineEmpty.ToArray();
                slots[4] = slot4;
                return HotbarViewModelFactory.Create(slots, PlacementOrigin.FromHotbarSlot(0), UIStateEnum.GameScreen, UiScreen.None, false);
            }

            #endregion
        }
    }
}
