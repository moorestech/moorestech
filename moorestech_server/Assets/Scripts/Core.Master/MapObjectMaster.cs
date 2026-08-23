using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.MapModule;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MapObjectMaster : IMasterValidator
    {
        public readonly Map Map;

        // 該当なしの戻り値。呼び出しごとに空集合を確保しない
        // Returned when nothing matches, so no empty set is allocated per call
        private static readonly HashSet<Guid> EmptyMapObjectGuids = new();

        // アイテム→落とすmapObject索引
        // earn item GUID → mapObjectGuids dropping that item
        private Dictionary<Guid, HashSet<Guid>> _mapObjectGuidsByEarnItem;

        public MapObjectMaster(JToken jToken)
        {
            Map = MapLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            return MapObjectMasterUtil.Validate(Map, out errorLogs);
        }

        public void Initialize()
        {
            MapObjectMasterUtil.Initialize(Map, out _mapObjectGuidsByEarnItem);
        }

        /// <summary>
        ///     ピンの狙い先指定を候補mapObjectGuid集合へ解決する（client/server共通の唯一の規則）
        ///     Resolves a pin target param into candidate mapObjectGuids; the single rule shared by client and server.
        /// </summary>
        public HashSet<Guid> ResolvePinTargets(MapObjectPinTutorialParam param)
        {
            if (!TryResolvePinTargets(param, out var pinTargets))
            {
                throw new InvalidOperationException($"Unknown pinTargetType: {param.PinTargetType}");
            }

            return pinTargets;
        }

        /// <summary>
        ///     未知の狙い先指定でも例外にせず解決可否を返す（マスタ検証は落ちずに報告する必要がある）
        ///     Reports whether the target param resolves instead of throwing, because master validation must report, not crash.
        /// </summary>
        public bool TryResolvePinTargets(MapObjectPinTutorialParam param, out HashSet<Guid> pinTargets)
        {
            switch (param.PinTargetParam)
            {
                case MapObjectPinTargetParam byMapObject:
                    pinTargets = new HashSet<Guid> { byMapObject.MapObjectGuid };
                    return true;
                // そのアイテムを落とす全mapObjectが候補。木の種類が増えてもマスタ側の列挙は不要
                // Every mapObject dropping the item is a candidate, so new tree species need no master enumeration
                case EarnItemPinTargetParam byEarnItem:
                    pinTargets = GetMapObjectGuidsByEarnItem(byEarnItem.ItemGuid);
                    return true;
                default:
                    pinTargets = EmptyMapObjectGuids;
                    return false;
            }
        }

        /// <summary>
        ///     そのアイテムをドロップする全マップオブジェクトのGUIDを取得（該当なしなら空）
        ///     Gets the GUIDs of every map object dropping the item (empty when none drops it).
        /// </summary>
        public HashSet<Guid> GetMapObjectGuidsByEarnItem(Guid itemGuid)
        {
            // Validateは他Masterより先にMapObjectMaster.Initializeが完了している前提で呼ばれる（MasterHolder.Loadのロード順に依存）
            // Validate assumes MapObjectMaster.Initialize already ran (relies on MasterHolder.Load's load order)
            if (_mapObjectGuidsByEarnItem == null)
            {
                throw new InvalidOperationException($"{nameof(MapObjectMaster)}.{nameof(Initialize)} was not called before {nameof(GetMapObjectGuidsByEarnItem)}.");
            }

            if (!_mapObjectGuidsByEarnItem.TryGetValue(itemGuid, out var mapObjectGuids)) return EmptyMapObjectGuids;

            return mapObjectGuids;
        }

        /// <summary>
        /// マップオブジェクトGUIDからマスターデータを取得（見つからない場合は例外）
        /// Gets the master data from the map object GUID (throws if not found).
        /// </summary>
        public MapObjectMasterElement GetMapObjectElement(Guid guid)
        {
            var result = GetMapObjectElementOrNull(guid);
            if (result == null)
            {
                throw new InvalidOperationException($"MapObjectElement not found. MapObjectGuid:{guid}");
            }
            return result;
        }

        /// <summary>
        /// マップオブジェクトGUIDからマスターデータを取得（見つからない場合はnull）
        /// Gets the master data from the map object GUID (returns null if not found).
        /// </summary>
        public MapObjectMasterElement GetMapObjectElementOrNull(Guid guid)
        {
            return Array.Find(Map.MapObjects, x => x.MapObjectGuid == guid);
        }
    }
}
