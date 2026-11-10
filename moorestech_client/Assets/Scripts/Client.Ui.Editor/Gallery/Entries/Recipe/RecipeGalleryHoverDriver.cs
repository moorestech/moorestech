using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Recipe
{
    /// <summary>
    /// 撮影の見本をホバーにする
    /// Puts a capture sample into hover
    /// </summary>
    public static class RecipeGalleryHoverDriver
    {
        public static T HoverWhenLaidOut<T>(T target) where T : VisualElement
        {
            // 載った時点では worldBound が 0 なので、寸法の決まる回を待って送る
            // worldBound is still zero on attach, so wait for the layout pass before sending
            target.RegisterCallback<AttachToPanelEvent>(_ => target.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged));
            return target;

            #region Internal

            void OnGeometryChanged(GeometryChangedEvent evt)
            {
                // silent-ok: 寸法 0 の回は中心が無いので次の回を待つ / ホバーが付かなければ Task 8 Step 3 の面の色の照合が止める
                // silent-ok: a zero-size pass has no centre so wait for the next / Task 8 Step 3 stops on the face colors if hover never applies
                if (target.worldBound.width <= 0f) return;
                target.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                // 寸法の通知の配信中にポインタを送ると、要素の下の判定が配置をやり直して配信中の一覧を書き換え例外になるので、次の更新へ回す
                // Sending during the geometry dispatch re-runs layout for picking and mutates the list being dispatched, so defer to the next update
                target.schedule.Execute(() => SendPointerMove(target.panel, target.worldBound.center));
            }

            void SendPointerMove(IPanel panel, Vector2 panelPosition)
            {
                // 本番の入力と同じくディスパッチャが要素の下のポインタを更新して :hover を付ける
                // As with real input, the dispatcher updates the element under the pointer and applies :hover
                var systemEvent = new Event { type = EventType.MouseMove, mousePosition = panelPosition };
                using var move = PointerMoveEvent.GetPooled(systemEvent);
                panel.visualTree.SendEvent(move);
            }

            #endregion
        }
    }
}
