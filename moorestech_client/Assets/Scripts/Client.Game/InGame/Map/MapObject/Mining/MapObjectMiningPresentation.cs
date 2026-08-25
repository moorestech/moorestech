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
    public static class MapObjectMiningPresentation
    {
        // 取得物ゼロの個体が多い
        // Many objects yield nothing
        private static readonly Guid[] NoEarnItemGuids = Array.Empty<Guid>();

        public static SoundEffectType GetDestroySoundType(MapObjectMasterElement element)
        {
            switch (element.SoundEffectType)
            {
                case MapObjectMasterElement.SoundEffectTypeConst.stone:
                    return SoundEffectType.DestroyStone;
                case MapObjectMasterElement.SoundEffectTypeConst.tree:
                    return SoundEffectType.DestroyTree;
                default:
                    Debug.LogError("採掘音が設定されていません");
                    return SoundEffectType.DestroyStone;
            }
        }

        public static IReadOnlyList<Guid> GetEarnItemGuids(MapObjectMasterElement element)
        {
            var earnItems = element.EarnItems;
            if (earnItems.Length == 0) return NoEarnItemGuids;

            var earnItemGuids = new Guid[earnItems.Length];
            for (var index = 0; index < earnItems.Length; index++) earnItemGuids[index] = earnItems[index].ItemGuid;
            return earnItemGuids;
        }
    }
}
