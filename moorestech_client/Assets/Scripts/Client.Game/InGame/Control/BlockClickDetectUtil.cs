using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.Control.ViewMode;
using UnityEngine;

namespace Client.Game.InGame.Control
{
    public static class BlockClickDetectUtil
    {
        public static bool TryGetCursorOnBlockPosition(out Vector3Int position)
        {
            position = Vector3Int.zero;
            
            if (!TryGetCursorOnBlock(out var blockObject)) return false;
            
            
            position = blockObject.BlockPosInfo.OriginalPos;
            
            return true;
        }
        
        public static bool TryGetCursorOnBlock(out BlockGameObject blockObject)
        {
            blockObject = null;
            
            if (!TryGetCursorOnComponent<BlockGameObjectChild>(out var child)) return false;
            
            blockObject = child.BlockGameObject;
            
            return true;
        }
        
        
        public static bool TryGetCursorOnElectricWire(out ElectricWireLineViewElement wireElement)
        {
            wireElement = null;

            var camera = Camera.main;
            if (camera == null) return false;

            // ワイヤーは専用レイヤのため単独Raycastで判定する
            // Wires live on a dedicated layer, so probe them with their own raycast
            var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.ElectricWireOnlyLayerMask)) return false;

            // ワイヤーコライダーは子オブジェクトのため親を辿って本体を得る
            // Wire colliders live on child objects, so climb to the parent to get the element
            wireElement = hit.collider.GetComponentInParent<ElectricWireLineViewElement>();
            return wireElement != null;
        }

        /// <summary>
        /// 25/11/4 列車エンティティとブロックのインタラクト判定の共通化のために一旦こうしたが、本当にこれで良いのだろうか、、、要検討
        /// </summary>
        public static bool TryGetCursorOnComponent<T>(out T component)
        {
            component = default;
            if (!TryGetFrontmostSolidHit(LayerConst.BlockOnlyLayerMask, RayDistance, out var hit)) return false;

            // 最前面ヒットの子要素から解決する
            // Resolve from the frontmost hit's children
            component = hit.collider.gameObject.GetComponentInChildren<T>();
            return component is not null;
        }

        public static bool TryGetCursorOnComponentInParent<T>(out T component)
        {
            component = default;
            if (!TryGetFrontmostSolidHit(LayerConst.BlockOnlyLayerMask, RayDistance, out var hit)) return false;

            // 列車の当たり判定コライダーは本体コンポーネントを子に持たないため親方向へ辿る
            // Train hit colliders do not hold the entity component in their children, so climb toward parents
            component = hit.collider.GetComponentInParent<T>();
            return component is not null;
        }

        private const float RayDistance = 100f;

        // 毎フレーム通る経路なので、ヒット配列は使い回してGCを出さない
        // This path runs every frame, so the hit array is reused instead of allocating
        private static RaycastHit[] HitBuffer = new RaycastHit[32];

        /// <summary>
        ///     照準レイの最前面の実体ヒットを返す。設置ゴーストのみ貫通する（InteractTargetSelectorと共通の規則）
        ///     Returns the aim ray's frontmost solid hit; only placement ghosts are penetrated (shared rule with InteractTargetSelector)
        /// </summary>
        public static bool TryGetFrontmostSolidHit(int layerMask, float maxDistance, out RaycastHit frontmostHit)
        {
            frontmostHit = default;

            // 25/11/4 そもそもCamera.mainを使ってていいのか？これも検討したい
            var camera = Camera.main;
            if (camera == null) return false;

            // 照準座標はAimPointProviderで視点モードに応じて一元解決する
            // The aim point is resolved centrally by AimPointProvider per view mode
            var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());

            var hitCount = RaycastNonAlloc(ray, layerMask, maxDistance);

            // 手前のプレビューゴーストだけを貫通対象にする。並べ替えずに最小距離を1回の走査で選ぶ
            // Only nearby preview ghosts are penetrated; the nearest is picked in one scan instead of sorting
            var found = false;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = HitBuffer[index];
                if (found && frontmostHit.distance <= hit.distance) continue;
                if (hit.collider.GetComponentInParent<BlockPreviewObject>() != null) continue;

                frontmostHit = hit;
                found = true;
            }

            return found;

            #region Internal

            // 飽和したまま返すと手前のヒットを取りこぼすため、バッファを倍にして採り直す
            // A saturated buffer could drop the nearest hit, so it is doubled and re-queried
            static int RaycastNonAlloc(Ray castRay, int mask, float distance)
            {
                while (true)
                {
                    var count = Physics.RaycastNonAlloc(castRay, HitBuffer, distance, mask);
                    if (count < HitBuffer.Length) return count;

                    HitBuffer = new RaycastHit[HitBuffer.Length * 2];
                }
            }

            #endregion
        }
    }
}
