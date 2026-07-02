using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
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
        /// 既存ポール同士の接続プレビューを計算する（状態③）
        /// Calculate preview for connecting two existing poles (state 3)
        /// </summary>
        public static GearChainPoleExtendPreviewData CalculatePoleToPole(Vector3Int fromPos, Vector3Int toPos, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory playerInventory, ItemId chainItemId)
        {
            if (!TryGetPoleInfo(fromPos, blockGameObjectDataStore, out var fromInfo)) return GearChainPoleExtendPreviewData.Invalid;
            if (!TryGetPoleInfo(toPos, blockGameObjectDataStore, out var toInfo)) return GearChainPoleExtendPreviewData.Invalid;

            // 既接続かどうかをクライアント側のステートから判定する
            // Determine existing connection from client-side state
            var alreadyConnected = fromInfo.PartnerInstanceIds.Contains(toInfo.InstanceId.AsPrimitive());

            var distance = Vector3Int.Distance(fromPos, toPos);
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(distance, fromInfo.MaxConnectionDistance, toInfo.MaxConnectionDistance, alreadyConnected, fromInfo.IsConnectionFull || toInfo.IsConnectionFull, chainItemId, playerInventory, ItemMaster.EmptyItemId);
            return new GearChainPoleExtendPreviewData(GetPoleCenter(fromPos), GetPoleCenter(toPos), judgement.IsPlaceable);
        }

        /// <summary>
        /// 起点ポールから新規設置位置への延長プレビューを計算する（状態④）
        /// Calculate preview for extending from a pole to a new placement position (state 4)
        /// </summary>
        public static GearChainPoleExtendPreviewData CalculateExtend(Vector3Int fromPos, Vector3Int placePos, GearChainPoleBlockParam placingPoleParam, ItemId poleItemId, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory playerInventory, ItemId chainItemId)
        {
            if (!TryGetPoleInfo(fromPos, blockGameObjectDataStore, out var fromInfo)) return GearChainPoleExtendPreviewData.Invalid;

            var distance = Vector3Int.Distance(fromPos, placePos);
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(distance, fromInfo.MaxConnectionDistance, placingPoleParam.MaxConnectionDistance, false, fromInfo.IsConnectionFull, chainItemId, playerInventory, poleItemId);
            return new GearChainPoleExtendPreviewData(GetPoleCenter(fromPos), GetPoleCenter(placePos), judgement.IsPlaceable);
        }

        /// <summary>
        /// インベントリから設置に使うポールアイテムを自動選択する（レールの橋脚選択と同方式）
        /// Auto-select a pole item from inventory (same approach as rail pier selection)
        /// </summary>
        public static bool TryFindPoleItemSlot(ILocalPlayerInventory playerInventory, out int slot, out ItemId poleItemId, out BlockMasterElement poleBlockMaster)
        {
            slot = -1;
            poleItemId = ItemMaster.EmptyItemId;
            poleBlockMaster = null;

            // サブインベントリを含まないメインインベントリのみ走査する（スロット番号のずれ防止）
            // Scan only the main inventory excluding sub inventories (prevents slot index mismatch)
            var mainSlotCount = Mathf.Min(playerInventory.Count, PlayerInventoryConst.MainInventorySize);
            for (var i = 0; i < mainSlotCount; i++)
            {
                var stack = playerInventory[i];
                if (!MasterHolder.BlockMaster.IsBlock(stack.Id)) continue;

                var blockId = MasterHolder.BlockMaster.GetBlockId(stack.Id);
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                if (blockMaster.BlockType != BlockMasterElement.BlockTypeConst.GearChainPole) continue;

                slot = i;
                poleItemId = stack.Id;
                poleBlockMaster = blockMaster;
                return true;
            }

            return false;
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

            info = new GearChainPoleClientInfo(blockObject.BlockInstanceId, param.MaxConnectionDistance, partnerIds.Length >= param.MaxConnectionCount, partnerIds);
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
