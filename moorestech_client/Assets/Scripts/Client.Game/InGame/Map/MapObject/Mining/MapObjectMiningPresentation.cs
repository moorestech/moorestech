using System;
using System.Collections.Generic;
using Client.Game.InGame.SoundEffect;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectマスタから採掘UIに見せる値を引く
    ///     Reads the values the mining UI shows from the map object master
    /// </summary>
    internal static class MapObjectMiningPresentation
    {
        // 取得物ゼロの個体が多い
        // Many objects yield nothing
        private static readonly Guid[] NoEarnItemGuids = Array.Empty<Guid>();

        public static SoundEffectType GetDestroySoundType(MapObjectMasterElement element)
        {
            // 装飾物は破壊されないため破壊音を持たない
            // A decoration is never destroyed, so it carries no destruction sound
            if (element.MiningParam is not IMinableMapObjectParam minableParam)
            {
                Debug.LogError("採掘音が設定されていません");
                return SoundEffectType.DestroyStone;
            }

            // 値集合はIMinableMapObjectParamが一度だけ定め、変種ごとの定数は同じ文字列を持つ
            // The value set is defined once on IMinableMapObjectParam, so every variant's constants hold the same strings
            switch (minableParam.SoundEffectType)
            {
                case MiningMiningParam.SoundEffectTypeConst.stone:
                    return SoundEffectType.DestroyStone;
                case MiningMiningParam.SoundEffectTypeConst.tree:
                    return SoundEffectType.DestroyTree;
                default:
                    Debug.LogError("採掘音が設定されていません");
                    return SoundEffectType.DestroyStone;
            }
        }

        public static IReadOnlyList<Guid> GetEarnItemGuids(MapObjectMasterElement element)
        {
            if (element.MiningParam is not IMinableMapObjectParam minableParam) return NoEarnItemGuids;

            var earnItems = minableParam.EarnItems.items;
            if (earnItems.Length == 0) return NoEarnItemGuids;

            var earnItemGuids = new Guid[earnItems.Length];
            for (var index = 0; index < earnItems.Length; index++) earnItemGuids[index] = earnItems[index].ItemGuid;
            return earnItemGuids;
        }
    }
}
