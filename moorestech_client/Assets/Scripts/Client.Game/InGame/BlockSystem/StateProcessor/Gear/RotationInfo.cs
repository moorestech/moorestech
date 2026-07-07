using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    public abstract class RotationInfo
    {
        [SerializeField] protected bool isReverse;
        [SerializeField] protected GearRotationDirectionMode directionMode;


        // TransformRotationInfoのみ値を返す。Simulator互換のためベースに公開
        // Only TransformRotationInfo returns a value. Exposed on base for simulator compatibility
        public virtual Transform RotationTransform => null;

        public abstract void Rotate(GearStateDetail gearStateDetail, float deltaTime);
    }

    public enum GearRotationDirectionMode
    {
        // ネットワーク回転方向とワールド符号規約に追従(かみ合う歯車・シャフト用)
        // Follow the network direction with the world-sign convention (meshing gears and shafts)
        NetworkSigned,

        // ネットワーク回転方向を無視して常に正転(ベルト表面等のゲームプレイ方向固定パーツ用)
        // Always run forward ignoring the network direction (gameplay-directional parts like belt surfaces)
        AlwaysForward,
    }

    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }

    public enum AnimationPlayDirection
    {
        Positive,
        Negative,
    }
}
