using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.Control.BuildView;
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
            // 最前面ヒットの子要素から解決する
            // Resolve from the frontmost hit's children
            return TryGetCursorOnHit(hit => hit.collider.gameObject.GetComponentInChildren<T>(), out component);
        }

        public static bool TryGetCursorOnComponentInParent<T>(out T component)
        {
            // 列車の当たり判定コライダーは本体コンポーネントを子に持たないため親方向へ辿る
            // Train hit colliders do not hold the entity component in their children, so climb toward parents
            return TryGetCursorOnHit(hit => hit.collider.GetComponentInParent<T>(), out component);
        }

        private static bool TryGetCursorOnHit<T>(System.Func<RaycastHit, T> resolveComponent, out T component)
        {
            component = default;

            // 25/11/4 そもそもCamera.mainを使ってていいのか？これも検討したい
            var camera = Camera.main;
            if (camera == null) return false;

            // 照準座標はAimPointProviderで視点モードに応じて一元解決する
            // The aim point is resolved centrally by AimPointProvider per view mode
            var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());

            var hits = Physics.RaycastAll(ray, 100, LayerConst.BlockOnlyLayerMask);
            if (hits.Length == 0) return false;

            // 手前のプレビューゴーストだけを貫通対象にする
            // Only nearby preview ghosts are allowed to be penetrated
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<BlockPreviewObject>() != null) continue;

                component = resolveComponent(hit);
                if (component is not null) return true;

                return false;
            }

            return false;
        }
    }
}
