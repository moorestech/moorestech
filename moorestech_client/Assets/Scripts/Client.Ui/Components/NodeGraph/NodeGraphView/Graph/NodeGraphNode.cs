using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// グラフの 1 ノード。座標は Y 上向きのツリー座標、カードは画面が作り自分の中心を点に合わせる
    /// One graph node; the position is in Y-up tree coordinates, and the screen-built card centers itself on the point
    /// </summary>
    /// <remarks>
    /// カードは押下を捕まえない・止めない。Button・Clickable を使わず、タップは NodeGraphView.OnNodeTapped で受ける（Web TreeView.tsx:17-18 相当）
    /// The card never captures or stops presses: do not use Button or Clickable, and receive taps on NodeGraphView.OnNodeTapped (like Web TreeView.tsx:17-18)
    /// </remarks>
    public sealed class NodeGraphNode
    {
        public string Key { get; }
        public Vector2 TreePosition { get; }
        public IReadOnlyList<string> PreviousKeys { get; }
        public VisualElement Card { get; }

        public NodeGraphNode(string key, Vector2 treePosition, IReadOnlyList<string> previousKeys, VisualElement card)
        {
            Key = key;
            TreePosition = treePosition;
            PreviousKeys = previousKeys;
            Card = card;
        }
    }
}
