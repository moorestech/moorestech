using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.GearChain
{
    public static class GearChainSystemUtil
    {
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            if (!TryGetGearChainPole(posA, out var poleA, out var transformerA) || !TryGetGearChainPole(posB, out var poleB, out var transformerB))
            {
                error = "InvalidTarget";
                return false;
            }

            // 同一ターゲットや距離超過を弾く
            // Reject same target or over distance
            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                error = "InvalidTarget";
                return false;
            }

            var maxDistance = Math.Min(poleA.MaxConnectionDistance, poleB.MaxConnectionDistance);
            if (Vector3Int.Distance(posA, posB) > maxDistance)
            {
                error = "TooFar";
                return false;
            }

            // 既存接続がある場合は失敗させる
            // Fail when already connected
            if (poleA.ContainsChainConnection(poleB.BlockInstanceId) || poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                error = "AlreadyConnected";
                return false;
            }

            // 接続数の上限を確認する
            // Ensure neither pole is at capacity
            if (poleA.IsConnectionFull || poleB.IsConnectionFull)
            {
                error = "ConnectionLimit";
                return false;
            }

            // 接続を確定させる
            // Finalize connection
            var addedA = poleA.TryAddChainConnection(poleB.BlockInstanceId);
            var addedB = addedA && poleB.TryAddChainConnection(poleA.BlockInstanceId);
            if (!addedA || !addedB)
            {
                poleA.RemoveChainConnection(poleB.BlockInstanceId);
                poleB.RemoveChainConnection(poleA.BlockInstanceId);
                error = "ConnectionLimit";
                return false;
            }

            RebuildNetworks(transformerA, transformerB);
            return true;
        }

        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            if (!TryGetGearChainPole(posA, out var poleA, out var transformerA) || !TryGetGearChainPole(posB, out var poleB, out var transformerB))
            {
                error = "InvalidTarget";
                return false;
            }

            // 相互接続でない場合は失敗
            // Fail when not connected to each other
            if (!poleA.ContainsChainConnection(poleB.BlockInstanceId) || !poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                error = "NotConnected";
                return false;
            }

            poleA.RemoveChainConnection(poleB.BlockInstanceId);
            poleB.RemoveChainConnection(poleA.BlockInstanceId);
            RebuildNetworks(transformerA, transformerB);
            return true;
        }

        private static bool TryGetGearChainPole(Vector3Int position, out IGearChainPole chainPole, out IGearEnergyTransformer transformer)
        {
            // 指定座標からコンポーネントを解決する
            // Resolve component from position
            chainPole = null;
            transformer = null;
            if (!ServerContext.WorldBlockDatastore.TryGetBlock(position, out IBlock block)) return false;
            chainPole = block.GetComponent<IGearChainPole>();
            transformer = block.GetComponent<IGearEnergyTransformer>();
            return chainPole != null && transformer != null;
        }

        private static void RebuildNetworks(params IGearEnergyTransformer[] transformers)
        {
            // ネットワークを再構築して回転を再計算する
            // Rebuild gear networks to recalc rotation
            
            foreach (var transformer in transformers) GearNetworkDatastore.RemoveGear(transformer);
            foreach (var transformer in transformers) GearNetworkDatastore.AddGear(transformer);
        }
    }
}

