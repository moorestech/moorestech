using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     mapObjectPin の pinTargetParam をピン候補の mapObjectGuid 集合へ解決する
    ///     Resolves a mapObjectPin's pinTargetParam into the set of candidate mapObjectGuids
    /// </summary>
    public static class MapObjectPinTargetResolver
    {
        public static IReadOnlyList<Guid> ResolveMapObjectGuids(MapObjectPinTutorialParam param)
        {
            return param.PinTargetParam switch
            {
                MapObjectPinTargetParam byMapObject => new[] { byMapObject.MapObjectGuid },
                EarnItemPinTargetParam byEarnItem => ResolveByEarnItem(byEarnItem.ItemGuid),
                _ => throw new InvalidOperationException($"Unknown pinTargetType: {param.PinTargetType}"),
            };
        }

        // そのアイテムを落とす全mapObjectが候補。木の種類が増えてもマスタ側の列挙は不要
        // Every mapObject dropping the item is a candidate, so new tree species need no master enumeration
        private static IReadOnlyList<Guid> ResolveByEarnItem(Guid itemGuid)
        {
            return MasterHolder.MapObjectMaster.Map.MapObjects
                .Where(mapObject => mapObject.EarnItems.Any(earnItem => earnItem.ItemGuid == itemGuid))
                .Select(mapObject => mapObject.MapObjectGuid)
                .ToList();
        }
    }
}
