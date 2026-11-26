using System.Collections.Generic;
using Client.Common.PrefabPostProcess.MeshMerge;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;

namespace Client.Game.InGame.Block.GearRotation.PrefabPostProcess
{
    /// <summary>
    /// 回す部品の値を頂点のUV6/UV7へ書く。並びはGearRotation.hlslと一致させる
    /// Writes the part's values into vertex UV6/UV7; the layout must match GearRotation.hlsl
    /// </summary>
    public sealed class GearRotationVertexWriter : IMergedVertexAttributeWriter
    {
        public const int AxisAndSpeedChannel = 6;
        public const int OriginAndModeChannel = 7;
        private static readonly int[] Channels = { AxisAndSpeedChannel, OriginAndModeChannel };

        private readonly Vector4 _axisAndSpeed;
        private readonly Vector4 _originAndMode;

        public GearRotationVertexWriter(TransformRotationInfo info, Vector3 blockOriginInHostSpace)
        {
            var axis = GearWorldRotationSign.ToAxisVector(info.Axis);
            _axisAndSpeed = new Vector4(axis.x, axis.y, axis.z, info.SignedSpeedFactor);
            _originAndMode = new Vector4(blockOriginInHostSpace.x, blockOriginInHostSpace.y, blockOriginInHostSpace.z, info.IsAlwaysForward ? 1f : 0f);
        }

        public IReadOnlyList<int> GetUvChannels()
        {
            return Channels;
        }

        // 部品の全頂点が同じ軸・速さ・ブロック原点を持つ
        // Every vertex of the part shares the same axis, speed and block origin
        public Vector4 GetSourceValue(int uvChannel, MeshRenderer source)
        {
            return uvChannel == AxisAndSpeedChannel ? _axisAndSpeed : _originAndMode;
        }
    }
}
