using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Common;

namespace Game.Gear.Tick
{
    // 原点generatorからtopologyをたどり、各gearの符号付き原点RPM比とループ矛盾を収集してcacheを構築する
    // Walks the topology from the origin generator, collecting per-gear signed origin RPM ratios and cycle conflicts into a cache
    public static class GearRotationTraversalBuilder
    {
        public static GearNetworkRotationCache Build(IGearGenerator origin)
        {
            var rotations = new Dictionary<BlockInstanceId, GearRotationRatio>();
            var rpmRatioConflicts = new List<float>();
            var hasDirectionConflict = false;

            // 原点は符号付き比 +1 で登録し、深さ優先で全接続をたどる
            // Register the origin at signed ratio +1, then walk all connections depth-first
            var originRotation = new GearRotationRatio(1f);
            rotations.Add(origin.BlockInstanceId, originRotation);
            foreach (var connect in origin.GetGearConnects())
            {
                hasDirectionConflict = Visit(connect, origin, originRotation);
                // 符号衝突（逆回転）はtopology起因で恒久なため探索を打ち切る
                // A sign conflict (reverse rotation) is permanent (topology-driven), so stop traversing
                if (hasDirectionConflict) break;
            }

            return new GearNetworkRotationCache(origin, hasDirectionConflict, rotations, rpmRatioConflicts);

            #region Internal

            bool Visit(GearConnect gearConnect, IGearEnergyTransformer connectSource, GearRotationRatio sourceRotation)
            {
                var transformer = gearConnect.Transformer;

                // 噛み合い接続なら符号反転、さらに歯車同士なら歯数比で大きさをスケールする
                // Meshing connections flip the sign; gear-to-gear meshes also scale the magnitude by the teeth ratio
                var isReverseRotation = gearConnect.Self.IsReverse && gearConnect.Target.IsReverse;
                float signedRatio;
                if (isReverseRotation)
                {
                    var magnitudeRatio = transformer is IGear gear && connectSource is IGear connectGear
                        ? (float)connectGear.TeethCount / gear.TeethCount
                        : 1f;
                    signedRatio = -sourceRotation.SignedRpmRatio * magnitudeRatio;
                }
                else
                {
                    signedRatio = sourceRotation.SignedRpmRatio;
                }

                // 訪問済みならループ閉合。符号が食い違えば逆回転衝突で恒久ロック、同符号での大きさ差は毎tick評価用に記録して探索を終える
                // A visited gear closes a cycle: a sign mismatch is a permanent reverse-rotation lock; same-sign magnitude differences are recorded for per-tick evaluation
                if (rotations.TryGetValue(transformer.BlockInstanceId, out var visited))
                {
                    if (visited.IsSameDirectionAsOrigin != (signedRatio >= 0f)) return true;

                    var magnitudeDiff = Math.Abs(Math.Abs(visited.SignedRpmRatio) - Math.Abs(signedRatio));
                    if (magnitudeDiff > 0f) rpmRatioConflicts.Add(magnitudeDiff);
                    return false;
                }

                // 原点以外のgeneratorは、算出した絶対回転方向（比の符号を原点の向きで解釈）が発電方向と食い違えば恒久ロック
                // Any non-origin generator locks permanently when its computed absolute direction (sign interpreted via the origin's direction) disagrees with its generating direction
                var computedClockwise = signedRatio >= 0f ? origin.GenerateIsClockwise : !origin.GenerateIsClockwise;
                if (transformer is IGearGenerator generator && generator.GenerateIsClockwise != computedClockwise)
                    return true;

                var rotation = new GearRotationRatio(signedRatio);
                rotations.Add(transformer.BlockInstanceId, rotation);

                // この歯車の接続先を再帰的にたどる
                // Recurse into the connections of this gear
                foreach (var connect in transformer.GetGearConnects())
                {
                    var conflicted = Visit(connect, transformer, rotation);
                    if (conflicted) return true;
                }

                return false;
            }

            #endregion
        }
    }
}
