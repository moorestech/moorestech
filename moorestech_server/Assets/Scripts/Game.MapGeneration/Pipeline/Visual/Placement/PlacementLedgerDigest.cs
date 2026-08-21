using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // 台帳を固定長のダイジェストへ畳む。見た目が配置に依存するようになったため、配置が1本でも動いたら見た目キャッシュを外す入力になる
    // Folds the ledger into a fixed-length digest; now that the visuals depend on placement, this is the input that drops the visual cache the moment a single placement moves
    public static class PlacementLedgerDigest
    {
        public static byte[] Compute(IReadOnlyList<LedgerPlacement> placements)
        {
            // 総順序を保つため全フィールドで比較する。台帳にはInstanceIdが無く、guid単独では同一種別の複数配置を区別できない
            // Compares every field to keep a total order; the ledger carries no InstanceId, and the guid alone cannot tell apart several placements of the same kind
            var ordered = new List<LedgerPlacement>(placements);
            ordered.Sort(CompareByAllFields);

            var digestSource = new List<byte>();
            foreach (var placement in ordered)
            {
                var guidBytes = System.Text.Encoding.UTF8.GetBytes(placement.Guid);
                digestSource.AddRange(BitConverter.GetBytes(guidBytes.Length));
                digestSource.AddRange(guidBytes);

                digestSource.AddRange(BitConverter.GetBytes(placement.ScenePosition.x));
                digestSource.AddRange(BitConverter.GetBytes(placement.ScenePosition.y));
                digestSource.AddRange(BitConverter.GetBytes(placement.ScenePosition.z));
                digestSource.AddRange(BitConverter.GetBytes(placement.Rotation.x));
                digestSource.AddRange(BitConverter.GetBytes(placement.Rotation.y));
                digestSource.AddRange(BitConverter.GetBytes(placement.Rotation.z));
                digestSource.AddRange(BitConverter.GetBytes(placement.Rotation.w));
                digestSource.AddRange(BitConverter.GetBytes(placement.Scale.x));
                digestSource.AddRange(BitConverter.GetBytes(placement.Scale.y));
                digestSource.AddRange(BitConverter.GetBytes(placement.Scale.z));
                digestSource.AddRange(BitConverter.GetBytes((int)placement.SurroundEffect));
                digestSource.AddRange(BitConverter.GetBytes(placement.ClusterId));
                digestSource.AddRange(BitConverter.GetBytes(placement.ClusterCenter.x));
                digestSource.AddRange(BitConverter.GetBytes(placement.ClusterCenter.y));
            }

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(digestSource.ToArray());

            #region Internal

            int CompareByAllFields(LedgerPlacement left, LedgerPlacement right)
            {
                var byGuid = string.CompareOrdinal(left.Guid, right.Guid);
                if (byGuid != 0) return byGuid;

                var byX = left.ScenePosition.x.CompareTo(right.ScenePosition.x);
                if (byX != 0) return byX;

                var byY = left.ScenePosition.y.CompareTo(right.ScenePosition.y);
                if (byY != 0) return byY;

                var byZ = left.ScenePosition.z.CompareTo(right.ScenePosition.z);
                if (byZ != 0) return byZ;

                var byRotationX = left.Rotation.x.CompareTo(right.Rotation.x);
                if (byRotationX != 0) return byRotationX;

                var byRotationY = left.Rotation.y.CompareTo(right.Rotation.y);
                if (byRotationY != 0) return byRotationY;

                var byRotationZ = left.Rotation.z.CompareTo(right.Rotation.z);
                if (byRotationZ != 0) return byRotationZ;

                var byRotationW = left.Rotation.w.CompareTo(right.Rotation.w);
                if (byRotationW != 0) return byRotationW;

                var byScaleX = left.Scale.x.CompareTo(right.Scale.x);
                if (byScaleX != 0) return byScaleX;

                var byScaleY = left.Scale.y.CompareTo(right.Scale.y);
                if (byScaleY != 0) return byScaleY;

                var byScaleZ = left.Scale.z.CompareTo(right.Scale.z);
                if (byScaleZ != 0) return byScaleZ;

                var byClusterId = left.ClusterId.CompareTo(right.ClusterId);
                if (byClusterId != 0) return byClusterId;

                var byClusterCenterX = left.ClusterCenter.x.CompareTo(right.ClusterCenter.x);
                return byClusterCenterX != 0
                    ? byClusterCenterX
                    : left.ClusterCenter.y.CompareTo(right.ClusterCenter.y);
            }

            #endregion
        }
    }
}
