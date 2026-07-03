using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーンポール接続・延長のプレビューを計算する。
    /// サーバーと同じ GearChainPlacementEvaluator を呼ぶことで判定の食い違いを防ぐ。
    /// Calculates preview data for gear chain pole connection and extension.
    /// Calls the same GearChainPlacementEvaluator as the server to prevent judgement mismatch.
    /// </summary>
    public static class GearChainPoleExtendPreviewCalculator
    {
        /// <summary>
        /// 既存ポール同士の接続プレビューを計算する（チェーンアイテムモード）
        /// Calculate preview for connecting two existing poles (chain item mode)
        /// </summary>
        public static GearChainPoleExtendPreviewData CalculatePoleToPole(Vector3Int fromPos, Vector3Int toPos, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory playerInventory, ItemId chainItemId)
        {
            if (!TryGetPoleInfo(fromPos, blockGameObjectDataStore, out var fromInfo)) return GearChainPoleExtendPreviewData.Invalid;
            if (!TryGetPoleInfo(toPos, blockGameObjectDataStore, out var toInfo)) return GearChainPoleExtendPreviewData.Invalid;

            // 既接続かをクライアントのステートで判定
            // Determine existing connection from client-side state
            var alreadyConnected = fromInfo.PartnerInstanceIds.Contains(toInfo.InstanceId.AsPrimitive());

            var distance = Vector3Int.Distance(fromPos, toPos);
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(distance, fromInfo.MaxConnectionDistance, toInfo.MaxConnectionDistance, alreadyConnected, fromInfo.IsConnectionFull || toInfo.IsConnectionFull, chainItemId, playerInventory, ItemMaster.EmptyItemId);
            return new GearChainPoleExtendPreviewData(GetPoleCenter(fromPos), GetPoleCenter(toPos), judgement.IsPlaceable);
        }

        /// <summary>
        /// 起点ポールから新規設置位置への延長プレビューを計算する（ポールアイテムモード）
        /// Calculate preview for extending from a pole to a new placement position (pole item mode)
        /// </summary>
        public static GearChainPoleExtendPreviewData CalculateExtend(Vector3Int fromPos, Vector3Int placePos, GearChainPoleBlockParam placingPoleParam, ItemId poleItemId, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory playerInventory, ItemId chainItemId)
        {
            if (!TryGetPoleInfo(fromPos, blockGameObjectDataStore, out var fromInfo)) return GearChainPoleExtendPreviewData.Invalid;

            // 新規ポール側は接続容量0の場合のみ上限超過として扱う
            // Treat the new pole as full only when its connection capacity is zero
            var anyConnectionFull = fromInfo.IsConnectionFull || placingPoleParam.MaxConnectionCount < 1;
            var distance = Vector3Int.Distance(fromPos, placePos);
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(distance, fromInfo.MaxConnectionDistance, placingPoleParam.MaxConnectionDistance, false, anyConnectionFull, chainItemId, playerInventory, poleItemId);
            return new GearChainPoleExtendPreviewData(GetPoleCenter(fromPos), GetPoleCenter(placePos), judgement.IsPlaceable);
        }

        /// <summary>
        /// クライアント側の情報からポールの判定用情報を解決する
        /// Resolve pole judgement info from client-side data
        /// </summary>
        public static bool TryGetPoleInfo(Vector3Int pos, BlockGameObjectDataStore blockGameObjectDataStore, out GearChainPoleClientInfo info)
        {
            info = default;

            if (!blockGameObjectDataStore.TryGetBlockGameObject(pos, out var blockObject)) return false;
            if (blockObject.BlockMasterElement.BlockParam is not GearChainPoleBlockParam param) return false;

            // ブロックステートから現在の接続先を取得する（未受信時は0接続とみなす）
            // Read current partners from block state (treat as 0 connections when not received yet)
            var stateDetail = blockObject.GetStateDetail<GearChainPoleStateDetail>(GearChainPoleStateDetail.BlockStateDetailKey);
            var partnerIds = stateDetail?.PartnerBlockInstanceIds ?? System.Array.Empty<int>();

            info = new GearChainPoleClientInfo(blockObject.BlockInstanceId, param.MaxConnectionDistance, param.MaxConnectionCount <= partnerIds.Length, partnerIds);
            return true;
        }

        public static Vector3 GetPoleCenter(Vector3Int blockPos)
        {
            return blockPos.AddBlockPlaceOffset();
        }
    }

    /// <summary>
    /// クライアント側で解決したポールの判定用情報
    /// Pole judgement info resolved on the client side
    /// </summary>
    public readonly struct GearChainPoleClientInfo
    {
        public readonly BlockInstanceId InstanceId;
        public readonly float MaxConnectionDistance;
        public readonly bool IsConnectionFull;
        public readonly int[] PartnerInstanceIds;

        public GearChainPoleClientInfo(BlockInstanceId instanceId, float maxConnectionDistance, bool isConnectionFull, int[] partnerInstanceIds)
        {
            InstanceId = instanceId;
            MaxConnectionDistance = maxConnectionDistance;
            IsConnectionFull = isConnectionFull;
            PartnerInstanceIds = partnerInstanceIds;
        }
    }

    /// <summary>
    /// 歯車チェーンポール延長プレビューの表示データ
    /// Display data for gear chain pole extension preview
    /// </summary>
    public readonly struct GearChainPoleExtendPreviewData
    {
        public static GearChainPoleExtendPreviewData Invalid => new(Vector3.zero, Vector3.zero, false, false);

        public readonly Vector3 StartPoint;
        public readonly Vector3 EndPoint;
        public readonly bool IsPlaceable;
        public readonly bool IsValid;

        public GearChainPoleExtendPreviewData(Vector3 startPoint, Vector3 endPoint, bool isPlaceable) : this(startPoint, endPoint, isPlaceable, true)
        {
        }

        private GearChainPoleExtendPreviewData(Vector3 startPoint, Vector3 endPoint, bool isPlaceable, bool isValid)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            IsPlaceable = isPlaceable;
            IsValid = isValid;
        }
    }
}
