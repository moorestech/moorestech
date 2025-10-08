#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public partial class RailGraphDatastore
    {
        public readonly struct RailGraphSnapshot
        {
            public IReadOnlyList<Node> Nodes { get; }
            public IReadOnlyList<Edge> Edges { get; }

            public RailGraphSnapshot(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
            {
                Nodes = nodes ?? Array.Empty<Node>();
                Edges = edges ?? Array.Empty<Edge>();
            }

            public readonly struct Node
            {
                public int Id { get; }
                public string Label { get; }
                public StationReference Station { get; }
                public ConnectionDestination Component { get; }

                public Node(int id, string label, StationReference station, ConnectionDestination component)
                {
                    Id = id;
                    Label = label;
                    Station = station;
                    Component = component;
                }
            }

            public readonly struct Edge
            {
                public int SourceId { get; }
                public int TargetId { get; }
                public int Distance { get; }

                public Edge(int sourceId, int targetId, int distance)
                {
                    SourceId = sourceId;
                    TargetId = targetId;
                    Distance = distance;
                }
            }
        }

        public static RailGraphSnapshot CreateSnapshot(Func<RailNode, string> labelSelector = null)
        {
            var instance = Instance;
            var nodes = new List<RailGraphSnapshot.Node>();
            var edges = new List<RailGraphSnapshot.Edge>();
            var seenEdges = new HashSet<(int, int)>();

            for (int id = 0; id < instance.railNodes.Count; id++)
            {
                var node = instance.railNodes[id];
                if (node == null)
                    continue;

                instance.railIdToComponentId.TryGetValue(id, out var component);
                string label = labelSelector?.Invoke(node) ?? BuildDefaultLabel(id, node, component);
                nodes.Add(new RailGraphSnapshot.Node(id, label, node.StationRef, component));

                foreach (var (neighborId, distance) in instance.connectNodes[id])
                {
                    if (neighborId < 0 || neighborId >= instance.railNodes.Count)
                        continue;
                    if (instance.railNodes[neighborId] == null)
                        continue;

                    var normalized = id < neighborId ? (id, neighborId) : (neighborId, id);
                    if (!seenEdges.Add(normalized))
                        continue;

                    edges.Add(new RailGraphSnapshot.Edge(normalized.Item1, normalized.Item2, distance));
                }
            }

            nodes.Sort((a, b) => a.Id.CompareTo(b.Id));
            edges.Sort((a, b) =>
            {
                int compare = a.SourceId.CompareTo(b.SourceId);
                if (compare != 0)
                    return compare;
                compare = a.TargetId.CompareTo(b.TargetId);
                if (compare != 0)
                    return compare;
                return a.Distance.CompareTo(b.Distance);
            });

            return new RailGraphSnapshot(nodes, edges);
        }

        public static void WriteJson(RailGraphSnapshot snapshot, string outputPath)
        {
            if (snapshot.Nodes == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (outputPath == null)
                throw new ArgumentNullException(nameof(outputPath));

            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"nodes\": [");

            for (int i = 0; i < snapshot.Nodes.Count; i++)
            {
                var node = snapshot.Nodes[i];
                builder.Append("    {");
                builder.Append("\"id\": ");
                builder.Append(node.Id);
                builder.Append(", \"label\": ");
                AppendJsonString(builder, node.Label ?? node.Id.ToString());
                builder.Append(", \"station\": ");
                AppendStationJson(builder, node.Station);
                builder.Append(", \"component\": ");
                AppendComponentJson(builder, node.Component);
                builder.Append("}");
                if (i < snapshot.Nodes.Count - 1)
                {
                    builder.Append(',');
                }
                builder.AppendLine();
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"edges\": [");

            for (int i = 0; i < snapshot.Edges.Count; i++)
            {
                var edge = snapshot.Edges[i];
                builder.Append("    {");
                builder.Append("\"source\": ");
                builder.Append(edge.SourceId);
                builder.Append(", \"target\": ");
                builder.Append(edge.TargetId);
                builder.Append(", \"distance\": ");
                builder.Append(edge.Distance);
                builder.Append("}");
                if (i < snapshot.Edges.Count - 1)
                {
                    builder.Append(',');
                }
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, builder.ToString());
        }

        private static string BuildDefaultLabel(int id, RailNode node, ConnectionDestination component)
        {
            var parts = new List<string> { $"#{id}" };

            var station = node.StationRef;
            if (station?.StationBlock != null)
            {
                var position = station.GetStationPosition();
                parts.Add($"Station {station.NodeRole}/{station.NodeSide}");
                parts.Add($"Pos {position.x},{position.y},{position.z}");
            }

            if (component?.DestinationID != null)
            {
                var dest = component.DestinationID;
                var pos = dest.Position;
                parts.Add($"Component {pos.x},{pos.y},{pos.z}#{dest.ID}");
                parts.Add(component.IsFront ? "Front" : "Back");
            }

            return string.Join("\\n", parts);
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(ch))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(ch);
                        }
                        break;
                }
            }
            builder.Append('"');
        }

        private static void AppendStationJson(StringBuilder builder, StationReference station)
        {
            if (station?.StationBlock == null)
            {
                builder.Append("null");
                return;
            }

            var position = station.GetStationPosition();
            builder.Append("{");
            builder.Append("\"role\": ");
            AppendJsonString(builder, station.NodeRole.ToString());
            builder.Append(", \"side\": ");
            AppendJsonString(builder, station.NodeSide.ToString());
            builder.Append(", \"position\": ");
            AppendVector3IntJson(builder, position);
            builder.Append("}");
        }

        private static void AppendComponentJson(StringBuilder builder, ConnectionDestination component)
        {
            if (component?.DestinationID == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append("{");
            builder.Append("\"position\": ");
            AppendVector3IntJson(builder, component.DestinationID.Position);
            builder.Append(", \"id\": ");
            builder.Append(component.DestinationID.ID);
            builder.Append(", \"isFront\": ");
            builder.Append(component.IsFront ? "true" : "false");
            builder.Append("}");
        }

        private static void AppendVector3IntJson(StringBuilder builder, Vector3Int value)
        {
            builder.Append("{");
            builder.Append("\"x\": ");
            builder.Append(value.x);
            builder.Append(", \"y\": ");
            builder.Append(value.y);
            builder.Append(", \"z\": ");
            builder.Append(value.z);
            builder.Append("}");
        }
    }
}
#endif
