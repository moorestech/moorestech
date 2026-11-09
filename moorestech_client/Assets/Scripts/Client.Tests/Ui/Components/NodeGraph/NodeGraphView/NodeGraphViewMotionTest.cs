using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// 速いドラッグの後の滑走と、中断・押下での停止（Web TreeViewViewportState.test.ts の 7・8 件目の写しと UITK の 1 件）
    /// Glide after a fast drag, and stopping on abort or press (copy of tests 7–8 of the Web TreeViewViewportState.test.ts plus one UITK test)
    /// </summary>
    public class NodeGraphViewMotionTest : NodeGraphTestStage
    {
        private const int MaxFrames = 500;

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">keeps gliding after a fast drag is released and eventually stops</web-test>
        [Test]
        public void 速いドラッグを離すと滑走しやがて止まる()
        {
            var view = MountGraph();
            FlingRight(view);
            var frames = RunUntilStill(view);
            Assert.Less(frames, MaxFrames, "the glide stops");
            Assert.That(ReadViewport(view).X, Is.GreaterThan(70f).And.LessThan(364f));
        }

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">aborts without gliding when the pointer is cancelled</web-test>
        [Test]
        public void ポインタが取り消されると滑走せず中断する()
        {
            var view = MountGraph();
            var start = DragRightFourTimes(view);

            // 捕捉を奪って中断させる（UITK の取消と同じ経路）
            // Steal the capture to abort (the same path as a UITK cancel)
            var thief = new VisualElement { name = "capture-thief" };
            rootVisualElement.Add(thief);
            thief.CapturePointer(PointerId.mousePointerId);
            simulate.MouseMove(start + new Vector2(64f, 0f));
            simulate.FrameUpdate();
            thief.ReleasePointer(PointerId.mousePointerId);
            simulate.MouseUp(start + new Vector2(64f, 0f), MouseButton.LeftMouse);
            for (var frame = 0; frame < 10; frame++) simulate.FrameUpdateMs(16);

            AssertViewport(64f, 0f, 1f, ReadViewport(view));
        }

        [Test]
        public void 右ボタンの押下でも滑走が止まる()
        {
            var view = MountGraph();
            FlingRight(view);
            simulate.FrameUpdateMs(16);
            simulate.FrameUpdateMs(16);

            // 受理されない押下でも滑走は止まる（Web の onPointerDown の包み）
            // Even an unaccepted press stops the glide (the Web onPointerDown wrapper)
            var point = ContentOrigin(view) + new Vector2(300f, 250f);
            simulate.MouseDown(point, MouseButton.RightMouse);
            simulate.MouseUp(point, MouseButton.RightMouse);
            var stoppedAt = ReadViewport(view).X;
            for (var frame = 0; frame < 10; frame++) simulate.FrameUpdateMs(16);

            Assert.Greater(stoppedAt, 64f, "it was gliding");
            Assert.AreEqual(stoppedAt, ReadViewport(view).X, Tolerance);
        }

        private NodeGraphView MountGraph()
        {
            var view = MountView(MountHost(400f, 300f));
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, Card("card-a")) }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            return view;
        }

        private Vector2 DragRightFourTimes(NodeGraphView view)
        {
            // 16ms ごとに x を 16・32・48・64 へ動かす（Web テストと同じ）
            // Move x to 16, 32, 48 and 64 every 16 ms (same as the Web test)
            var start = ContentOrigin(view) + new Vector2(20f, 20f);
            simulate.MouseDown(start, MouseButton.LeftMouse);
            for (var step = 1; step <= 4; step++)
            {
                simulate.FrameUpdateMs(16);
                simulate.MouseMove(start + new Vector2(16f * step, 0f));
            }
            return start;
        }

        private void FlingRight(NodeGraphView view)
        {
            var start = DragRightFourTimes(view);
            simulate.FrameUpdateMs(6);
            simulate.MouseUp(start + new Vector2(64f, 0f), MouseButton.LeftMouse);
        }

        private int RunUntilStill(NodeGraphView view)
        {
            // 3 フレーム続けて動かなければ止まったとみなす
            // Treat three motionless frames in a row as stopped
            var previous = ReadViewport(view).X;
            var stillFrames = 0;
            for (var frame = 1; frame <= MaxFrames; frame++)
            {
                simulate.FrameUpdateMs(16);
                var x = ReadViewport(view).X;
                stillFrames = Mathf.Approximately(x, previous) ? stillFrames + 1 : 0;
                previous = x;
                if (3 <= stillFrames) return frame;
            }
            return MaxFrames;
        }
    }
}
