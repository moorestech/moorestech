using System;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Research.Tree;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// 研究ツリーのグラフの形の比較（NodeGraphView へ渡し直す判定。チャレンジと同じ基準）（L0）
    /// Research tree graph-shape comparison (the NodeGraphView re-handing check, same criteria as challenges) (L0)
    /// </summary>
    public class ResearchTreeGraphShapeTest
    {
        private static readonly Guid Node1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid Node2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        [Test]
        public void グラフの形は並びと座標と前提と注目点で比べカードの状態と選択では変わらない()
        {
            var first = Card(Node1, Vector2.zero, false);
            var second = Card(Node2, new Vector2(220f, 0f), false, Node1);
            var shown = Tree(ResearchFocusPoint.At(Vector2.zero), first, second);

            // 選択・状態だけの違いはカードの描き直しで足り、同じ形
            // A selection or state-only difference needs just a card redraw, so the shape is the same
            Assert.IsTrue(shown.HasSameGraphAs(Tree(ResearchFocusPoint.At(Vector2.zero), first, Card(Node2, new Vector2(220f, 0f), true, Node1))));

            // 座標・前提・並び・注目点が変われば違う形
            // Position, prerequisite, order or focus changes differ
            Assert.IsFalse(shown.HasSameGraphAs(Tree(ResearchFocusPoint.At(Vector2.zero), first, Card(Node2, new Vector2(240f, 0f), false, Node1))), "座標");
            Assert.IsFalse(shown.HasSameGraphAs(Tree(ResearchFocusPoint.At(Vector2.zero), first, Card(Node2, new Vector2(220f, 0f), false))), "前提");
            Assert.IsFalse(shown.HasSameGraphAs(Tree(ResearchFocusPoint.At(Vector2.zero), second, first)), "並び");
            Assert.IsFalse(shown.HasSameGraphAs(Tree(ResearchFocusPoint.At(new Vector2(220f, 0f)), first, second)), "注目点");
            Assert.IsFalse(shown.HasSameGraphAs(Tree(ResearchFocusPoint.None, first, second)), "注目点の有無");
        }

        private static ResearchTreeViewModel Tree(ResearchFocusPoint focus, params ResearchNodeCardViewModel[] nodes)
        {
            return ResearchTreeViewModel.Create(nodes, focus);
        }

        private static ResearchNodeCardViewModel Card(Guid guid, Vector2 position, bool selected, params Guid[] previous)
        {
            return new ResearchNodeCardViewModel(guid, new ItemId(1), position, previous, new NodeCardState(selected, false, false), LocalizationKeys.Ui.Research.Completed, selected);
        }
    }
}
