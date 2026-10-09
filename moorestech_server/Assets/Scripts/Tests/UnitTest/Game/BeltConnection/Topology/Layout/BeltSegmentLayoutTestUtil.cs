using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Topology.Layout
{
    internal static class BeltSegmentLayoutTestUtil
    {
        internal static BeltSegmentLayout[] BuildLayouts(IWorldBlockDatastore world)
        {
            var layouts = BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(world));
            // 番号は並び順そのもの
            // The index must equal the list position
            for (var i = 0; i < layouts.Length; i++) Assert.AreEqual(i, layouts[i].Index, "index equals list position");
            return layouts;
        }

        internal static void AssertSegment(BeltSegmentLayout layout, BeltSegmentKind kind, int speed, BeltDirection forward, params Vector3Int[] cells)
        {
            Assert.AreEqual(kind, layout.Kind, $"kind of segment {layout.Index}");
            Assert.IsFalse(layout.IsInternal, $"internal flag of segment {layout.Index}");
            Assert.AreEqual(speed, layout.Speed, $"speed of segment {layout.Index}");
            Assert.AreEqual(forward, layout.Forward, $"forward of segment {layout.Index}");
            CollectionAssert.AreEqual(cells, Positions(layout), $"cells of segment {layout.Index}");
            Assert.AreEqual(cells.Length, layout.Capacity, $"capacity of segment {layout.Index}");
        }

        internal static void AssertInternal(BeltSegmentLayout layout, BeltDirection forward)
        {
            Assert.IsTrue(layout.IsInternal, $"internal flag of segment {layout.Index}");
            Assert.AreEqual(BeltSegmentKind.Normal, layout.Kind, $"kind of internal segment {layout.Index}");
            Assert.IsEmpty(layout.Cells, $"cells of internal segment {layout.Index}");
            Assert.AreEqual(1, layout.Capacity, $"capacity of internal segment {layout.Index}");
            Assert.AreEqual(BeltConstants.MaxSpeed, layout.Speed, $"speed of internal segment {layout.Index}");
            Assert.AreEqual(forward, layout.Forward, $"forward of internal segment {layout.Index}");
            Assert.AreEqual(1, layout.Inputs.Length, $"input count of internal segment {layout.Index}");
            Assert.AreEqual(1, layout.Outputs.Length, $"output count of internal segment {layout.Index}");
        }

        internal static void AssertLink(BeltSegmentLayoutLink link, BeltDirection direction, BeltEntryDirection entryDirection, int partner)
        {
            Assert.AreEqual(direction, link.Direction, "link direction");
            Assert.AreEqual(entryDirection, link.EntryDirection, "link entry direction");
            Assert.AreEqual(partner, link.PartnerSegmentIndex, "link partner segment");
        }

        internal static Vector3Int[] Positions(BeltSegmentLayout layout)
        {
            return layout.Cells.Select(cell => cell.Position).ToArray();
        }

        // インスタンスIDを除いた比較用の文字列。別ワールド同士の決定性比較に使う
        // Comparison string without instance ids, used to compare determinism across separate worlds
        internal static List<string> Signature(BeltSegmentLayout[] layouts)
        {
            return layouts.Select(layout =>
                $"{layout.Index} {layout.Kind} internal={layout.IsInternal} speed={layout.Speed} fwd={layout.Forward} " +
                $"cells=[{string.Join(",", Positions(layout))}] in=[{string.Join(",", layout.Inputs.Select(Describe))}] " +
                $"out=[{string.Join(",", layout.Outputs.Select(Describe))}]").ToList();

            string Describe(BeltSegmentLayoutLink link) => $"{link.Direction}/{link.EntryDirection}/{link.PartnerSegmentIndex}/{link.Connection.PartnerCell}";
        }
    }
}
