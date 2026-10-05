using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
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
        
        
        public static ConnectionLineAimResult GetCursorOnConnectionLine()
        {
            // 照準レイの生成とコライダー→線本体の解決は正本を呼ぶ（Task 10）
            // Ray creation and collider-to-line resolution call their single definitions (Task 10)
            if (!TryCreateAimRay(out var ray)) return ConnectionLineAimResult.Missed(ConnectionLineAimOutcome.NoCamera);

            // 接続線は専用レイヤのため単独Raycastで判定する
            // Connection lines live on a dedicated layer, so probe them with their own raycast
            if (!Physics.Raycast(ray, out var hit, AimRayDistance, LayerConst.ConnectionLineOnlyLayerMask, QueryTriggerInteraction.Collide)) return ConnectionLineAimResult.Missed(ConnectionLineAimOutcome.NothingHit);

            var line = ConnectionLineDeleteTarget.FromCollider(hit.collider);
            return line == null ? ConnectionLineAimResult.Missed(ConnectionLineAimOutcome.NotALine) : ConnectionLineAimResult.Found(line);
        }

        /// <summary>
        /// 25/11/4 列車エンティティとブロックのインタラクト判定の共通化のために一旦こうしたが、本当にこれで良いのだろうか、、、要検討
        /// </summary>
        public static bool TryGetCursorOnComponent<T>(out T component)
        {
            component = default;
            if (!TryGetFrontmostSolidHit(LayerConst.BlockOnlyLayerMask, AimRayDistance, out var hit)) return false;

            // 最前面ヒットの子要素から解決する
            // Resolve from the frontmost hit's children
            component = hit.collider.gameObject.GetComponentInChildren<T>();
            return component is not null;
        }

        public static bool TryGetCursorOnComponentInParent<T>(out T component)
        {
            component = default;
            if (!TryGetFrontmostSolidHit(LayerConst.BlockOnlyLayerMask, AimRayDistance, out var hit)) return false;

            // 列車の当たり判定コライダーは本体コンポーネントを子に持たないため親方向へ辿る
            // Train hit colliders do not hold the entity component in their children, so climb toward parents
            component = hit.collider.GetComponentInParent<T>();
            return component is not null;
        }

        public const float AimRayDistance = 100f;

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

            var hitCount = RaycastAimAll(layerMask, maxDistance, out var hits);

            // 手前のプレビューゴーストだけを貫通対象にする。並べ替えずに最小距離を1回の走査で選ぶ
            // Only nearby preview ghosts are penetrated; the nearest is picked in one scan instead of sorting
            var found = false;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = hits[index];
                if (found && frontmostHit.distance <= hit.distance) continue;
                if (hit.collider.GetComponentInParent<BlockPreviewObject>() != null) continue;

                frontmostHit = hit;
                found = true;
            }

            return found;
        }

        /// <summary>
        ///     照準レイで全ヒットし件数を返す。hitsは件数分のみ有効
        ///     Raycasts all hits along the aim ray; hits is valid up to the count
        /// </summary>
        public static int RaycastAimAll(int layerMask, float maxDistance, out RaycastHit[] hits)
        {
            hits = HitBuffer;
            if (!TryCreateAimRay(out var ray)) return 0;

            // 飽和したまま返すと手前のヒットを取りこぼすため、バッファを倍にして採り直す
            // A saturated buffer could drop the nearest hit, so it is doubled and re-queried
            while (true)
            {
                var count = Physics.RaycastNonAlloc(ray, HitBuffer, maxDistance, layerMask, QueryTriggerInteraction.Collide);
                hits = HitBuffer;
                if (count < HitBuffer.Length) return count;

                HitBuffer = new RaycastHit[HitBuffer.Length * 2];
            }
        }

        // 照準レイを作る。カメラが無ければfalse
        // Build the aim ray; false without a camera
        private static bool TryCreateAimRay(out Ray ray)
        {
            ray = default;
            var camera = Camera.main;
            if (camera == null) return false;
            ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            return true;
        }
    }
    /// <summary>
    ///     接続線への照準結果。外れた理由を区別する（スポイトの外れは通常操作なのでログは出さない）
    ///     Aim result on a connection line; distinguishes why it missed (an eyedropper miss is normal, so it is not logged)
    /// </summary>
    public enum ConnectionLineAimOutcome
    {
        Found,
        NoCamera,
        NothingHit,
        NotALine,
    }

    public readonly struct ConnectionLineAimResult
    {
        public ConnectionLineAimOutcome Outcome { get; }
        public ConnectionLineDeleteTarget Line { get; }

        private ConnectionLineAimResult(ConnectionLineAimOutcome outcome, ConnectionLineDeleteTarget line)
        {
            Outcome = outcome;
            Line = line;
        }

        public static ConnectionLineAimResult Found(ConnectionLineDeleteTarget line)
        {
            return new ConnectionLineAimResult(ConnectionLineAimOutcome.Found, line);
        }

        public static ConnectionLineAimResult Missed(ConnectionLineAimOutcome outcome)
        {
            return new ConnectionLineAimResult(outcome, null);
        }
    }

}
