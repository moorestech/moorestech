using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Common;

namespace Game.Gear.Tick
{
    // 原点generatorに対する各gearの符号付き原点RPM比。原点=+1、噛み合い反転で符号反転、逆向きは負。
    // 実RPM = |比| × 原点RPM、回転方向 = 比の符号（原点の向き基準）
    // Signed origin RPM ratio of a gear. Origin = +1, sign flips on meshing reversal, reverse is negative.
    // Actual RPM = |ratio| × origin RPM, direction = sign relative to the origin
    public readonly struct GearRotationRatio
    {
        public readonly float SignedRpmRatio;

        public GearRotationRatio(float signedRpmRatio)
        {
            SignedRpmRatio = signedRpmRatio;
        }

        // 原点の向きを基準にした回転方向。原点(=+1)と同符号なら原点と同じ向き
        // Rotation direction relative to the origin; same sign as origin (+1) means same direction
        public bool IsSameDirectionAsOrigin => SignedRpmRatio >= 0f;
    }

    // network 1つ分のtraversal cache。符号衝突（逆回転）はtopology起因で恒久、大きさ矛盾は原点RPM依存なので毎tick評価する。
    // 原点generatorへの参照を保持し、実RPMの導出（|比|×GenerateRpm）と絶対向き（符号×GenerateIsClockwise）をO(1)で行えるようにする。
    // Traversal cache for one network. Holds a reference to the origin generator so actual RPM (|ratio|×GenerateRpm)
    // and absolute direction (sign×GenerateIsClockwise) can be derived in O(1). Sign conflicts are permanent; magnitude conflicts are per-tick.
    public class GearNetworkRotationCache
    {
        public IGearGenerator Origin { get; }
        public BlockInstanceId OriginBlockInstanceId => Origin.BlockInstanceId;
        public bool HasDirectionConflict { get; }

        private readonly Dictionary<BlockInstanceId, GearRotationRatio> _rotations;

        // ループ閉合時に検出した同符号どうしの比の大きさの差。|差| × 原点RPM が閾値を超えるとロック
        // Absolute magnitude differences of same-sign ratios found at cycle closures; locks when |diff| × origin rpm exceeds the threshold
        private readonly List<float> _rpmRatioConflicts;

        // 旧実装のRPM一致判定閾値(0.1f)をそのまま踏襲する
        // Inherit the legacy rpm equality threshold (0.1f) as-is
        private const float RpmConflictThreshold = 0.1f;

        public GearNetworkRotationCache(IGearGenerator origin, bool hasDirectionConflict, Dictionary<BlockInstanceId, GearRotationRatio> rotations, List<float> rpmRatioConflicts)
        {
            Origin = origin;
            HasDirectionConflict = hasDirectionConflict;
            _rotations = rotations;
            _rpmRatioConflicts = rpmRatioConflicts;
        }

        public bool TryGetRotation(BlockInstanceId blockInstanceId, out GearRotationRatio rotation)
        {
            return _rotations.TryGetValue(blockInstanceId, out rotation);
        }

        public GearRotationRatio GetRotation(BlockInstanceId blockInstanceId)
        {
            return _rotations[blockInstanceId];
        }

        public bool IsRpmConflicted(float originRpm)
        {
            foreach (var ratioDiff in _rpmRatioConflicts)
                if (ratioDiff * originRpm > RpmConflictThreshold)
                    return true;

            return false;
        }
    }
}
