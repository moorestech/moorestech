using System.Collections.Generic;
using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 直線ベルコンの Renderer を無効にし、分岐器モデルを別オブジェクトとして出している状態
    // The state of one covered belt: its renderers disabled and the splitter model shown as a separate object
    public sealed class BlockGameObjectBeltMergeCellCover : IBeltMergeCellCover
    {
        private readonly BlockGameObject _belt;
        private readonly GameObject _coverObject;
        private readonly List<Renderer> _hiddenRenderers;

        // Unity の破棄済み判定(== null)に頼る。ベルコンが撤去されると false
        // Relies on Unity's destroyed check (== null); false once the belt was removed
        public bool IsAlive => _belt != null;

        public BlockGameObjectBeltMergeCellCover(BlockGameObject belt, GameObject coverObject, List<Renderer> hiddenRenderers)
        {
            _belt = belt;
            _coverObject = coverObject;
            _hiddenRenderers = hiddenRenderers;
        }

        public void Remove()
        {
            // ベルコンが生きていれば描画を戻す。覆いは常に消す
            // Restore the belt's rendering when it still exists; the cover object always goes
            foreach (var renderer in _hiddenRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }
            Object.Destroy(_coverObject);
        }
    }
}
