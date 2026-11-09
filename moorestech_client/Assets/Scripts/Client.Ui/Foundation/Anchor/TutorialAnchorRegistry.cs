using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Anchor
{
    // 木を照会してアンカーを解決する。木から外れた要素は照会に現れないので、状態を持たずにDetach同期を満たす
    // Resolves anchors by querying the tree; detached elements vanish from the query, so Detach sync needs no state
    public static class TutorialAnchorRegistry
    {
        public static TutorialAnchorResolution Resolve(VisualElement searchRoot, string anchorId)
        {
            // マーカー付きの要素のうち、IDのクラスを持つものを集める
            // Collect marked elements that carry the ID class
            VisualElement found = null;
            var count = 0;
            foreach (var marked in searchRoot.Query(className: TutorialAnchor.MarkerClassName).ToList())
            {
                if (!marked.ClassListContains(anchorId)) continue;
                found ??= marked;
                count++;
            }

            // 0件・複数件の報告は、状態の遷移を知っている呼ぶ側（TutorialAnchorTracker）が出す。毎フレーム呼ばれるのでここでは黙る
            // Callers that know state transitions (TutorialAnchorTracker) report zero or multiple matches; this runs every frame so it stays silent
            if (count == 0) return new TutorialAnchorResolution.Missing();
            if (1 < count) return new TutorialAnchorResolution.Duplicate(count);
            return new TutorialAnchorResolution.Unique(found);
        }
    }
}
