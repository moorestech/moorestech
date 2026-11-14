using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Filter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Filter
{
    /// <summary>
    /// 物理画素の群が倍率に依らず置き場の箱どおりに中身を置き、当たり判定を変えないか（L1・設計書 A-226）
    /// Checks the physical-pixel group places content on the host box and keeps hit testing at any scale (L1, design A-226)
    /// </summary>
    public class PixelFilterGroupTest : UiTestHost
    {
        private static readonly Rect HostRect = new(100f, 50f, 40f, 30f);
        private static readonly Rect ChildRect = new(5f, 4f, 10f, 6f);

        [Test]
        public void 埋める形は倍率に依らず子を置き場の箱どおりに置き当たり判定も変わらない([Values(1f, 1.5f)] float scale)
        {
            pixelsPerPoint = scale;
            var (host, group, child) = MountFill();

            // 外層 1/倍率・内層 倍率
            // Outer layer at 1/scale, inner at scale; the visible box equals the host
            Assert.AreEqual(1f / scale, group.resolvedStyle.scale.value.x, 0.0001f);
            Assert.AreEqual(scale, group.contentContainer.resolvedStyle.scale.value.x, 0.0001f);
            AssertRect(host.worldBound, group.contentContainer.worldBound, "content box");

            // 子の上は子、子の外の置き場の中は置き場、置き場の外はどちらでもない
            // Over the child picks the child, inside the host but off the child picks the host, outside picks neither
            var panel = rootVisualElement.panel;
            Assert.AreSame(child, panel.Pick(child.worldBound.center));
            Assert.AreSame(host, panel.Pick(new Vector2(HostRect.xMax - 2f, HostRect.yMax - 2f)));
            var outside = panel.Pick(new Vector2(HostRect.xMax + 2f, HostRect.yMax + 2f));
            Assert.AreNotSame(host, outside);
            Assert.AreNotSame(child, outside);

            // 子の箱は群なしで同じ子を置いた箱と同じ（倍率 1.5 の画素への丸めも含めて）
            // The child box equals the same child placed without a group (pixel rounding at 1.5 included)
            AssertRect(MountPlainBaseline(new Vector2(ChildRect.x, ChildRect.y), new Vector2(ChildRect.width, ChildRect.height)), child.worldBound, "child box");
        }

        [Test]
        public void 流れに入る形は中身の大きさで置き場を広げる([Values(1f, 1.5f)] float scale)
        {
            pixelsPerPoint = scale;
            var host = new VisualElement();
            host.style.position = Position.Absolute;
            host.style.left = HostRect.x;
            host.style.top = HostRect.y;
            var group = new PixelFilterGroup(PixelFilterGroupFlow.Flow);
            host.Add(group);
            var child = Box(37f, 23f);
            group.Add(child);
            rootVisualElement.Add(host);
            simulate.FrameUpdate();

            // 変形は layout に効かないので、置き場は中身の 37×23 になり、見た目も同じ位置
            // Transforms do not affect layout, so the host becomes the content's 37x23 and the visible box matches
            var baseline = Box(37f, 23f);
            baseline.style.position = Position.Absolute;
            baseline.style.left = HostRect.x;
            baseline.style.top = HostRect.y;
            rootVisualElement.Add(baseline);
            simulate.FrameUpdate();
            AssertRect(baseline.layout, host.layout, "host box");
            AssertRect(baseline.worldBound, child.worldBound, "child box");
        }

        [Test]
        public void 載ったまま倍率が変わると層の拡大を付け直す()
        {
            pixelsPerPoint = 1f;
            var (host, group, child) = MountFill();
            Assert.AreEqual(1f, group.resolvedStyle.scale.value.x, 0.0001f);

            pixelsPerPoint = 1.5f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(1f / 1.5f, group.resolvedStyle.scale.value.x, 0.0001f);
            Assert.AreEqual(1.5f, group.contentContainer.resolvedStyle.scale.value.x, 0.0001f);
            AssertRect(host.worldBound, group.contentContainer.worldBound, "content box after the change");
            Assert.AreSame(child, rootVisualElement.panel.Pick(child.worldBound.center));
        }

        [Test]
        public void 置き場からはみ出す子も置き場の外の同じ位置に置く([Values(1f, 1.5f)] float scale)
        {
            pixelsPerPoint = scale;
            var (host, group, _) = MountFill();
            var overflow = Box(6f, 6f);
            overflow.style.position = Position.Absolute;
            overflow.style.left = -3f;
            overflow.style.top = -3f;
            group.Add(overflow);
            simulate.FrameUpdate();
            AssertRect(MountPlainBaseline(new Vector2(-3f, -3f), new Vector2(6f, 6f)), overflow.worldBound, "overflow box");
        }

        private (VisualElement host, PixelFilterGroup group, VisualElement child) MountFill()
        {
            var host = new VisualElement();
            host.style.position = Position.Absolute;
            host.style.left = HostRect.x;
            host.style.top = HostRect.y;
            host.style.width = HostRect.width;
            host.style.height = HostRect.height;
            var group = new PixelFilterGroup(PixelFilterGroupFlow.Fill);
            host.Add(group);
            var child = Box(ChildRect.width, ChildRect.height);
            child.style.position = Position.Absolute;
            child.style.left = ChildRect.x;
            child.style.top = ChildRect.y;
            group.Add(child);
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            return (host, group, child);
        }

        // 群なしで同じ置き場に同じ子を置いた箱（倍率 1.5 の画素への丸めも含めた基準）
        // The same child in the same host without a group (the reference, including pixel rounding at scale 1.5)
        private Rect MountPlainBaseline(Vector2 position, Vector2 size)
        {
            var host = new VisualElement();
            host.style.position = Position.Absolute;
            host.style.left = HostRect.x;
            host.style.top = HostRect.y;
            host.style.width = HostRect.width;
            host.style.height = HostRect.height;
            var child = Box(size.x, size.y);
            child.style.position = Position.Absolute;
            child.style.left = position.x;
            child.style.top = position.y;
            host.Add(child);
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            return child.worldBound;
        }

        private static VisualElement Box(float width, float height)
        {
            var box = new VisualElement();
            box.style.width = width;
            box.style.height = height;
            box.style.flexShrink = 0;
            return box;
        }

        private static void AssertRect(Rect expected, Rect actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 0.01f, $"{what} x");
            Assert.AreEqual(expected.y, actual.y, 0.01f, $"{what} y");
            Assert.AreEqual(expected.width, actual.width, 0.01f, $"{what} width");
            Assert.AreEqual(expected.height, actual.height, 0.01f, $"{what} height");
        }
    }
}
