using System.Collections.Generic;
using Mooresmaster.LocalizationCsv;

namespace mooresmaster.Generator.Localization;

public sealed class LocalizationKeyNode
{
    public string Segment = "";
    public string FullKey = "";
    public List<LocalizationKeyNode> Children = new();
    public bool IsLeaf;
}

public static class LocalizationKeyTree
{
    public static LocalizationKeyNode Build(LocalizationRow[] rows)
    {
        var root = new LocalizationKeyNode();
        foreach (var row in rows)
        {
            var node = root;
            var segments = row.Key.Split('.');

            // 入力順を維持しながら各セグメントを木へ挿入する
            // Insert each segment into the tree while preserving input order
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                if (node.IsLeaf)
                {
                    throw new LocalizationCsvException($"Key '{node.FullKey}' is both a leaf and a branch");
                }

                var segment = segments[segmentIndex];
                var child = FindChild(node, segment);
                if (child == null)
                {
                    child = new LocalizationKeyNode
                    {
                        Segment = segment,
                        FullKey = CreateFullKey(node, segment),
                    };
                    node.Children.Add(child);
                }

                node = child;
            }

            // 子を持つ既存ノードは新たな葉として受け入れない
            // Reject an existing branch when the same node is declared as a leaf
        if (0 < node.Children.Count)
            {
                throw new LocalizationCsvException($"Key '{node.FullKey}' is both a leaf and a branch");
            }

            node.IsLeaf = true;
        }

        return root;

        #region Internal

        LocalizationKeyNode? FindChild(LocalizationKeyNode parent, string segment)
        {
            foreach (var child in parent.Children)
            {
                if (child.Segment == segment)
                {
                    return child;
                }
            }

            return null;
        }

        string CreateFullKey(LocalizationKeyNode parent, string segment)
        {
            if (parent == root)
            {
                return segment;
            }

            return $"{parent.FullKey}.{segment}";
        }

        #endregion
    }
}
