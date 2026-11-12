using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジのカテゴリ1つの表示（線は同じカテゴリの前提だけ）
    /// Display of one challenge category (edges only to prerequisites in the same category)
    /// </summary>
    public sealed class ChallengeCategoryViewModel
    {
        public Guid Guid { get; }
        public IReadOnlyList<ChallengeNodeCardViewModel> Nodes { get; }

        private ChallengeCategoryViewModel(Guid guid, IReadOnlyList<ChallengeNodeCardViewModel> nodes)
        {
            Guid = guid;
            Nodes = nodes;
        }

        public static ChallengeCategoryViewModel Create(Guid guid, IReadOnlyList<ChallengeNodeCardViewModel> nodes)
        {
            // カテゴリの中のキーを集める
            // Collect the keys inside the category
            var keys = new HashSet<Guid>();
            foreach (var node in nodes) keys.Add(node.Guid);

            // 別カテゴリの前提は線にしない（Web も描かない。NodeGraphView はグラフに無い親を LogError にするので渡す前に除く。C4-d D-5）
            // Prerequisites in other categories get no edge (the Web draws none; NodeGraphView logs an error for a missing parent, so drop them first; C4-d D-5)
            var linked = new List<ChallengeNodeCardViewModel>(nodes.Count);
            foreach (var node in nodes)
            {
                var previous = new List<Guid>(node.PreviousGuids.Count);
                // silent-ok: 別カテゴリの前提に線を引かないのは Web と同じ仕様 / L0 ChallengeListViewModelFactoryTest が除くことを固定する
                foreach (var previousGuid in node.PreviousGuids) if (keys.Contains(previousGuid)) previous.Add(previousGuid);
                linked.Add(new ChallengeNodeCardViewModel(node.Guid, node.TreePosition, node.Scale, previous, node.State));
            }
            return new ChallengeCategoryViewModel(guid, linked);
        }

        public bool TryGetInitialFocus(out Vector2 treePoint)
        {
            // 注目点は進行中のノードの座標
            // The focus point is the in-progress node position
            var found = ChallengeListLogic.TryFindInitialFocusNode(Nodes, out var node);
            treePoint = found ? node.TreePosition : Vector2.zero;
            return found;
        }

        // グラフの形（並び順のノードの置き方と注目点）が同じか。部品へ中身を渡し直すかの判定の 1 本
        // Whether the graph shape (node placements in order and the focus) matches; the single check for handing content to the component again
        public bool HasSameGraphAs(ChallengeCategoryViewModel other)
        {
            if (Nodes.Count != other.Nodes.Count) return false;
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (!Nodes[i].HasSameGraphPlacementAs(other.Nodes[i])) return false;
            }
            var hasFocus = TryGetInitialFocus(out var focus);
            var otherHasFocus = other.TryGetInitialFocus(out var otherFocus);
            return hasFocus == otherHasFocus && focus == otherFocus;
        }
    }
}
