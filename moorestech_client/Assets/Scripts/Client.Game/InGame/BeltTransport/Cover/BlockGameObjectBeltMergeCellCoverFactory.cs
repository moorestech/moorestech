using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マスのベルコンと同型同速の分岐器 prefab を同位置・同回転で出し、ベルコンの Renderer を無効にする
    // Shows the splitter prefab of the belt's type and speed at the belt's position and rotation, disabling the belt's renderers
    // 覆いは BlockGameObject の子にせず専用の親に置く(ベルコン撤去時は IsAlive で追従する。AddressableBeltItemObjectFactory と同じ親方式)
    // Covers live under a dedicated parent rather than the BlockGameObject (removal is tracked via IsAlive; same parent scheme as AddressableBeltItemObjectFactory)
    public sealed class BlockGameObjectBeltMergeCellCoverFactory : IBeltMergeCellCoverFactory
    {
        private const string ParentName = "BeltMergeCellCovers";
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        // 分岐器が引けなかったベルト種別。毎フレームの再試行でログを繰り返さないために覚える
        // Belt kinds with no resolvable splitter, remembered so the per-frame retry does not repeat the log
        private readonly HashSet<BlockId> _unresolvedBeltBlockIds = new();
        private Transform _parent;

        public BlockGameObjectBeltMergeCellCoverFactory(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public bool TryCreate(Vector3Int position, out IBeltMergeCellCover cover)
        {
            cover = null;
            if (!_blockGameObjectDataStore.TryGetBlockGameObject(position, out var belt)) return false;
            if (_unresolvedBeltBlockIds.Contains(belt.BlockId)) return false;
            if (!BeltSplitterCounterpart.TryGetSplitterBlockId(belt.BlockId, out var splitterBlockId))
            {
                _unresolvedBeltBlockIds.Add(belt.BlockId);
                return false;
            }

            var coverObject = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(splitterBlockId, belt.transform.position, belt.transform.rotation);
            coverObject.transform.SetParent(Parent(), true);
            StripBlockBehaviour(coverObject, splitterBlockId);
            cover = new BlockGameObjectBeltMergeCellCover(belt, coverObject, HideRenderers(belt));
            return true;

            #region Internal

            // 見た目だけが欲しいので、ブロックとしての振る舞い(当たり判定・状態処理・プレビュー専用物)は外す
            // Only the visuals are wanted, so block behaviour (colliders, state processors, preview-only objects) is stripped
            void StripBlockBehaviour(GameObject target, BlockId blockId)
            {
                foreach (var collider in target.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
                foreach (var behaviour in target.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is IBlockStateChangeProcessor || behaviour is BlockGameObject || behaviour is BlockGameObjectChild) Object.Destroy(behaviour);
                }
                BlockPreviewOnlyObjects.Initialize(target, blockId, false);
            }

            // 有効だった Renderer だけ無効にして覚える。戻すときに元から無効だったものを有効化しないため
            // Disable and remember only the renderers that were enabled, so restoring never enables ones that were off to begin with
            List<Renderer> HideRenderers(BlockGameObject target)
            {
                var hidden = new List<Renderer>();
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled) continue;
                    renderer.enabled = false;
                    hidden.Add(renderer);
                }
                return hidden;
            }

            Transform Parent()
            {
                if (_parent == null) _parent = new GameObject(ParentName).transform;
                return _parent;
            }

            #endregion
        }
    }
}
