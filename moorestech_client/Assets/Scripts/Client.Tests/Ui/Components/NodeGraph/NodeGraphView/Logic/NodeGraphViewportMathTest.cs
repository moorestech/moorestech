using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Motion.Inertia;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.NodeGraph.Logic
{
    /// <summary>
    /// ズーム・中央寄せ・寸法変化・慣性の計算（Web viewport.test.ts の写し）
    /// Zoom, centering, resize and inertia math (copy of the Web viewport.test.ts)
    /// </summary>
    public class NodeGraphViewportMathTest
    {
        private const float Tolerance = 1e-3f;

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">keeps the world point below the cursor fixed</web-test>
        [Test]
        public void カーソルの下のワールドの点を動かさない()
        {
            var result = NodeGraphViewportMath.ZoomAt(new NodeGraphViewport(10f, 20f, 1f), new Vector2(110f, 120f), -100f);
            Assert.AreEqual(100f, (110f - result.X) / result.Scale, Tolerance);
            Assert.AreEqual(100f, (120f - result.Y) / result.Scale, Tolerance);
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">clamps zoom scale</web-test>
        [Test]
        public void 倍率を範囲に丸める()
        {
            Assert.AreEqual(0.4f, NodeGraphViewportMath.ZoomAt(NodeGraphViewport.Identity, Vector2.zero, 100000f).Scale);
            Assert.AreEqual(2.5f, NodeGraphViewportMath.ZoomAt(NodeGraphViewport.Identity, Vector2.zero, -100000f).Scale);
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">places the canvas point at the viewport center</web-test>
        [Test]
        public void キャンバスの点をビューポートの中央に置く()
        {
            var result = NodeGraphViewportMath.CenterOn(new Vector2(800f, 200f), new Vector2(400f, 300f), 1f);
            AssertViewport(-600f, -50f, 1f, result);
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">accounts for scale when centering</web-test>
        [Test]
        public void 中央寄せは倍率を考える()
        {
            var result = NodeGraphViewportMath.CenterOn(new Vector2(100f, 100f), new Vector2(400f, 300f), 2f);
            Assert.AreEqual(200f, result.X + 100f * 2f, Tolerance);
            Assert.AreEqual(150f, result.Y + 100f * 2f, Tolerance);
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">縮んだぶんの半分だけ平行移動し、中央に見えていた点を中央に保つ</web-test>
        [Test]
        public void 縮んだ分の半分だけ平行移動し中央の点を保つ()
        {
            var before = NodeGraphViewportMath.CenterOn(new Vector2(200f, 200f), new Vector2(1248f, 484f), 1f);
            var after = NodeGraphViewportMath.ShiftForResize(before, new Vector2(1248f, 484f), new Vector2(1248f, 449f));
            var expected = NodeGraphViewportMath.CenterOn(new Vector2(200f, 200f), new Vector2(1248f, 449f), 1f);
            AssertViewport(expected.X, expected.Y, expected.Scale, after);
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">拡縮率は変えない</web-test>
        [Test]
        public void 寸法変化で倍率は変えない()
        {
            var result = NodeGraphViewportMath.ShiftForResize(new NodeGraphViewport(10f, 20f, 1.5f), new Vector2(100f, 100f), new Vector2(140f, 60f));
            AssertViewport(30f, 0f, 1.5f, result);
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">decays speed to 1/e after one time constant</web-test>
        [Test]
        public void 時定数1つで速さが1割るeに落ちる()
        {
            // 滑走は向きを固定した 1 次元。(1,-2) の速さ √5 で作り、τ の前後 1ms の変位の差を速さとして読む
            // The glide is 1-D along a fixed direction; build it with speed √5 of (1,-2) and read speed as the displacement change across 1 ms around τ
            var velocity = new Vector2(1f, -2f);
            var step = new RecordingInertiaStep();
            var glide = NodeGraphPanVelocity.CreateGlide(velocity.magnitude, step);
            glide.Begin();
            glide.Advance(NodeGraphPanVelocity.FrictionTauMs - 0.5f);
            glide.Advance(NodeGraphPanVelocity.FrictionTauMs + 0.5f);
            var speedAtTau = step.Offsets[1] - step.Offsets[0];
            var direction = velocity.normalized;
            Assert.AreEqual(Mathf.Exp(-1f), direction.x * speedAtTau, 1e-3f);
            Assert.AreEqual(-2f * Mathf.Exp(-1f), direction.y * speedAtTau, 1e-3f);
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">blends toward the instantaneous velocity without overshooting</web-test>
        [Test]
        public void 瞬間の速度へ行き過ぎずに寄せる()
        {
            var blended = NodeGraphPanVelocity.Blend(Vector2.zero, new Vector2(16f, -8f), 16f);
            Assert.That(blended.x, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(blended.y, Is.LessThan(0f).And.GreaterThan(-0.5f));
        }

        /// <web-test file="src/shared/treeView/viewport/viewport.test.ts">clamps the fling speed while preserving direction</web-test>
        [Test]
        public void 滑走の速さを向きを保って上限に丸める()
        {
            var clamped = NodeGraphPanVelocity.Clamp(new Vector2(30f, 40f));
            Assert.AreEqual(NodeGraphPanVelocity.MaxFlingSpeed, clamped.magnitude, Tolerance);
            Assert.AreEqual(40f / 30f, clamped.y / clamped.x, Tolerance);
            Assert.AreEqual(new Vector2(0.5f, 0f), NodeGraphPanVelocity.Clamp(new Vector2(0.5f, 0f)));
        }

        private static void AssertViewport(float x, float y, float scale, NodeGraphViewport actual)
        {
            Assert.AreEqual(x, actual.X, Tolerance, "x");
            Assert.AreEqual(y, actual.Y, Tolerance, "y");
            Assert.AreEqual(scale, actual.Scale, Tolerance, "scale");
        }

        private sealed class RecordingInertiaStep : IUiInertiaStep
        {
            public readonly List<float> Offsets = new();

            public void Apply(float offset)
            {
                Offsets.Add(offset);
            }
        }
    }
}
