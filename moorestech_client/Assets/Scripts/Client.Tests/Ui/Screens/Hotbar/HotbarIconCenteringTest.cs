using Client.Ui.Components.Slots;
using Client.Ui.Screens.Hotbar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 配置対象の絵が枠の中央に描かれる（L1。Web placementIconCentering.spec.ts のホットバー）
    /// The placement picture is drawn centred in the slot (L1; the hotbar case of Web placementIconCentering.spec.ts)
    /// </summary>
    public class HotbarIconCenteringTest : ScreenTestHost
    {
        private const string IconImageClass = "game-icon__image";

        /// <web-test file="e2e/tests/regression/placementIconCentering.spec.ts">ホットバーのアイコンは枠の中央に描かれる</web-test>
        [Test]
        public void ホットバーのアイコンは枠の中央に描かれる()
        {
            var view = Mount(new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(), Toaster));
            view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // 描く面は GameIcon の絵の要素（絵の箱の padding を除いた内容箱いっぱい）。正方の絵はそこへ収まる
            // The drawn area is the GameIcon image element (filling the icon's content box without padding); a square picture fits it
            var slot = Find<SlotFrame>(view, "hotbar-slot-0");
            var image = slot.Q(className: IconImageClass);
            Assert.IsNotNull(image);
            var drawnWorld = image.worldBound;
            var left = drawnWorld.xMin - slot.worldBound.xMin;
            var right = slot.worldBound.xMax - drawnWorld.xMax;
            var top = drawnWorld.yMin - slot.worldBound.yMin;
            var bottom = slot.worldBound.yMax - drawnWorld.yMax;
            Assert.Greater(left, 0f);
            Assert.Less(Mathf.Abs(left - right), 0.5f);
            Assert.Less(Mathf.Abs(top - bottom), 0.5f);
            Assert.Less(Mathf.Abs(left - top), 0.5f);
        }

        [Test]
        public void 同じBPの撮り直しで枠の絵が替わる()
        {
            // BP の絵は参照の等価なので、Model が組み直した VM は撮り直しのたびに配られる。ここでは配られた VM で絵が替わることを見る（計画 D-19）
            // A blueprint icon has reference equality, so the model's rebuilt VM is pushed on each recapture; here we check the pushed VM swaps the picture (plan D-19)
            var thumbnails = HotbarTestPresentation.CreateThumbnails();
            var view = Mount(new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(thumbnails), Toaster));
            view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            thumbnails.TryGet(HotbarTestPresentation.PicturedBlueprintId, out var first);
            Assert.AreSame(first, IconTexture(view));

            // 撮り直しは同じ Guid への 2 度目の Add
            // A recapture is a second Add for the same Guid
            var recaptured = HotbarTestPresentation.CreateSquare(new Color32(51, 153, 204, 255));
            thumbnails.Add(HotbarTestPresentation.PicturedBlueprintId, recaptured);
            view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            Assert.AreNotSame(first, IconTexture(view));
            Assert.AreSame(recaptured, IconTexture(view));
        }

        private Texture2D IconTexture(HotbarHudView view)
        {
            return Find<SlotFrame>(view, "hotbar-slot-0").Q(className: IconImageClass).resolvedStyle.backgroundImage.texture;
        }
    }
}
