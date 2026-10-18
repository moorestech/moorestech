using Client.Game.InGame.Block;
using Client.Game.InGame.Block.DistanceCulling;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.ColliderStreaming.Block
{
    /// <summary>
    /// ブロックの設置・削除を共通登録へ委ねる
    /// Delegates block placement and removal to the shared registrar
    /// </summary>
    public sealed class BlockColliderCullingRegisterService : IStartable, IBlockDistanceCullingSource
    {
        private readonly BlockDistanceCullingRegistrar _registrar;

        public BlockColliderCullingRegisterService(BlockGameObjectDataStore dataStore, ColliderDistanceCullingManager manager)
        {
            _registrar = new BlockDistanceCullingRegistrar(dataStore, manager.Culling, this);
        }

        public void Start()
        {
            _registrar.Initialize();
        }

        public BlockDistanceCullingEntry CreateEntry(BlockGameObject block)
        {
            // 有効コライダー範囲と対象を登録
            // Register enabled collider bounds and the target
            var colliders = block.GetComponentsInChildren<Collider>(true);
            return new BlockDistanceCullingEntry(CalcWorldBounds(colliders, block.transform.position), new BlockColliderCullingTarget(colliders));

            #region Internal

            Bounds CalcWorldBounds(Collider[] targetColliders, Vector3 fallbackCenter)
            {
                // 有効かつアクティブなコライダーのみで足場を算出する。無効/非アクティブは原点の縮退boundsを返し足場を壊すため除外
                // Build the footprint from enabled+active colliders only; disabled/inactive ones return origin-degenerate bounds that corrupt it
                var encapsulated = false;

                // 初期値は有効コライダーが無い場合のフォールバック（ブロック原点の1チャンク＝旧来の点登録相当）
                // Initial value is the fallback when no valid collider exists (single chunk at block origin, like the legacy point registration)
                var result = new Bounds(fallbackCenter, Vector3.zero);
                foreach (var collider in targetColliders)
                {
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    if (!encapsulated)
                    {
                        result = collider.bounds;
                        encapsulated = true;
                    }
                    else
                    {
                        result.Encapsulate(collider.bounds);
                    }
                }

                return result;
            }

            #endregion
        }
    }
}
