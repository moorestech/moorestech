using UnityEngine;
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

            // 0件と複数件は開発者ログへ出す（呼び出し側のチュートリアルが枠を出せない原因になる）
            // Log zero and multiple matches for developers (they explain why a tutorial cannot show its frame)
            if (count == 0)
            {
                Debug.LogWarning($"[TutorialAnchorRegistry] anchor is missing: {anchorId}");
                return new TutorialAnchorResolution.Missing();
            }
            if (1 < count)
            {
                Debug.LogWarning($"[TutorialAnchorRegistry] anchor is duplicated ({count}): {anchorId}");
                return new TutorialAnchorResolution.Duplicate(count);
            }
            return new TutorialAnchorResolution.Unique(found);
        }
    }
}
