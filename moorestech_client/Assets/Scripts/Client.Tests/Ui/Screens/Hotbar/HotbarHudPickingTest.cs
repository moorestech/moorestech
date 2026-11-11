using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Hotbar.Parts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 帯は入力を奪わず、列だけが拾ってホイール素通しの印を持つ（L1。Web alwaysOnHudWheel.test.ts・bd albof.30）
    /// The band never takes input; only the row picks and carries the wheel pass-through mark (L1; Web alwaysOnHudWheel.test.ts, bd albof.30)
    /// </summary>
    public class HotbarHudPickingTest : ScreenTestHost
    {
        private HotbarHudView _view;

        [SetUp]
        public void MountHotbar()
        {
            _view = Mount(new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void ホットバー帯は入力を奪わない()
        {
            // 帯は画面幅いっぱいだが、列の外の何も無い所は画面の下へ素通す
            // The band spans the screen width, yet empty space outside the row passes through
            Assert.AreEqual(PickingMode.Ignore, _view.pickingMode);
            var row = Find<VisualElement>(_view, "hotbar-grid");
            var picked = _view.panel.Pick(new Vector2(row.worldBound.xMin - 40f, row.worldBound.center.y));
            Assert.IsFalse(picked == _view || (picked != null && _view.Contains(picked)), picked == null ? "null" : picked.name);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">クリックを持つスロット列だけが入力を取り戻し、ホイールは素通しの印を持つ</web-test>
        [Test]
        public void クリックを持つスロット列だけが入力を取り戻しホイール素通しの印を持つ()
        {
            var row = Find<VisualElement>(_view, "hotbar-grid");
            Assert.AreEqual(PickingMode.Position, row.pickingMode);
            Assert.IsTrue(row.ClassListContains(WheelPassthroughMark.ClassName));
            Assert.IsFalse(_view.ClassListContains(WheelPassthroughMark.ClassName));

            // 読み手の判定: 枠の上（列の子孫）は素通し、帯そのものは素通しでない（Web isWheelPassthrough の closest）
            // The reader's check: over a slot (a row descendant) passes through, the band itself does not (the Web isWheelPassthrough closest)
            Assert.IsTrue(WheelPassthroughMark.IsPassthrough(Find<VisualElement>(_view, "hotbar-slot-1")));
            Assert.IsFalse(WheelPassthroughMark.IsPassthrough(_view));
            Assert.IsFalse(WheelPassthroughMark.IsPassthrough(null));

            // 枠の隙間も列が拾う（Web の .hotbarFrame は pointer-events: auto）
            // The row also picks the gaps between slots (the Web .hotbarFrame is pointer-events: auto)
            var gap = new Vector2(Find<VisualElement>(_view, "hotbar-slot-1").worldBound.xMax + 2.5f, row.worldBound.center.y);
            Assert.AreEqual(row, _view.panel.Pick(gap));
        }
    }
}
