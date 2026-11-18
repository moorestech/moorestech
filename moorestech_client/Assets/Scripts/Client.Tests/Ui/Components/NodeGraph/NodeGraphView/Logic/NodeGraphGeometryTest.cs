using System;
using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.NodeGraph.Logic
{
    /// <summary>
    /// ツリー座標とキャンバス座標の写像・境界・線（Web treeGeometry.test.ts の写し）
    /// Tree-to-canvas mapping, bounds and lines (copy of the Web treeGeometry.test.ts)
    /// </summary>
    public class NodeGraphGeometryTest
    {
        private const float Tolerance = 1e-4f;

        /// <web-test file="src/shared/treeView/treeGeometry.test.ts">maps Y-up tree coordinates into CSS canvas coordinates</web-test>
        [Test]
        public void Y上向きのツリー座標を下向きのキャンバス座標へ写す()
        {
            var bounds = NodeGraphGeometry.ComputeCanvasBounds(new[] { new Vector2(-100f, 50f), new Vector2(100f, -50f) }, 20f);
            AssertBounds(240f, 140f, 120f, 70f, bounds);
            var point = NodeGraphGeometry.ToCanvasPoint(new Vector2(-100f, 50f), bounds);
            Assert.AreEqual(20f, point.x, Tolerance);
            Assert.AreEqual(20f, point.y, Tolerance);
        }

        /// <web-test file="src/shared/treeView/treeGeometry.test.ts">returns a padded canvas for an empty tree</web-test>
        [Test]
        public void 空のツリーは余白だけのキャンバスになる()
        {
            AssertBounds(40f, 40f, 20f, 20f, NodeGraphGeometry.ComputeCanvasBounds(Array.Empty<Vector2>(), 20f));
        }

        /// <web-test file="src/shared/treeView/treeGeometry.test.ts">creates a line rooted at the child point</web-test>
        [Test]
        public void 線は子の点を始点にする()
        {
            var line = NodeGraphGeometry.LineBetween(new Vector2(2f, 3f), new Vector2(2f, 13f));
            Assert.AreEqual(2f, line.Origin.x, Tolerance);
            Assert.AreEqual(3f, line.Origin.y, Tolerance);
            Assert.AreEqual(10f, line.Length, Tolerance);
            Assert.AreEqual(90f, line.AngleDegrees, Tolerance);
        }

        private static void AssertBounds(float width, float height, float offsetX, float offsetY, NodeGraphCanvasBounds actual)
        {
            Assert.AreEqual(width, actual.Width, Tolerance, "width");
            Assert.AreEqual(height, actual.Height, Tolerance, "height");
            Assert.AreEqual(offsetX, actual.OffsetX, Tolerance, "offsetX");
            Assert.AreEqual(offsetY, actual.OffsetY, Tolerance, "offsetY");
        }
    }
}
