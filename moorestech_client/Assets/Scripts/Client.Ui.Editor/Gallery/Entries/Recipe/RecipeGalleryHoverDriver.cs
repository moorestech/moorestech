using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Recipe
{
    /// <summary>
    /// 見本をホバーにする
    /// Puts a sample into hover
    /// </summary>
    public static class RecipeGalleryHoverDriver
    {
        public static T HoverWhenLaidOut<T>(T target) where T : VisualElement
        {
            // 載った時点では worldBound が 0 なので、寸法の決まる回を待って送る。外れたら待ちを解く
            // worldBound is still zero on attach, so wait for the layout pass; stop waiting once detached
            target.RegisterCallback<AttachToPanelEvent>(_ => target.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged));
            target.RegisterCallback<DetachFromPanelEvent>(_ => target.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged));
            return target;

            #region Internal

            void OnGeometryChanged(GeometryChangedEvent evt)
            {
                // silent-ok: 寸法 0 の回は中心が無いので次の回を待つ / ホバーが付かなければギャラリーの見本の面の色で開発者が気づく
                // silent-ok: a zero-size pass has no centre so wait for the next / a developer sees the sample's face color if hover never applies
                if (target.worldBound.width <= 0f) return;
                target.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                // 寸法の通知の配信中にポインタを送ると、要素の下の判定が配置をやり直して配信中の一覧を書き換え例外になるので、次の更新へ回す
                // Sending during the geometry dispatch re-runs layout for picking and mutates the list being dispatched, so defer to the next update
                target.schedule.Execute(SendPointerMove);
            }

            void SendPointerMove()
            {
                // 待つ間に見本が外れたら送り先が無いので、理由を残して送らない
                // If the sample left the panel while waiting there is nowhere to send, so log why and skip
                if (target.panel == null)
                {
                    Debug.LogWarning($"[UiGallery] hover skipped: {target.name} left the panel before the pointer move");
                    return;
                }
                // 本番の入力と同じくディスパッチャが要素の下のポインタを更新して :hover を付ける
                // As with real input, the dispatcher updates the element under the pointer and applies :hover
                var systemEvent = new Event { type = EventType.MouseMove, mousePosition = target.worldBound.center };
                using var move = PointerMoveEvent.GetPooled(systemEvent);
                target.panel.visualTree.SendEvent(move);
            }

            #endregion
        }
    }
}
