using System;
using System.Linq;
using Core.Master;
using Game.SaveLoad.Pruning.Items;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Sections
{
    /// <summary>trainUnits節: マスタに無い貨車を含む列車と、除去したブロックのレールに載る列車を除去し、残った貨車のコンテナ内アイテム参照を空にする</summary>
    /// <summary>The trainUnits section: removes trains holding a car absent from the master or sitting on a pruned block's rail, then empties item references in the remaining cars' containers</summary>
    /// <summary>綴りはTrainUnitSaveData/TrainCarSaveData/RailPositionSaveDataのプロパティ名（JsonProperty無し）に一致させる</summary>
    /// <summary>Spellings match the property names of TrainUnitSaveData/TrainCarSaveData/RailPositionSaveData, which carry no JsonProperty</summary>
    public sealed class TrainUnitsSectionPruner : IRemovedBlockAwareSectionPruner
    {
        private const string CarsKey = "Cars";
        private const string TrainCarMasterIdKey = "TrainCarMasterId";
        private const string TrainCarInstanceIdKey = "TrainCarInstanceId";
        private const string ContainerSaveDataKey = "ContainerSaveData";
        private const string TrainUnitInstanceIdKey = "TrainUnitInstanceId";
        private const string RailPositionSaveDataKey = "railPositionSaveData";
        private const string RailSnapshotKey = "RailSnapshot";

        public string SaveSectionName => "trainUnits";

        public MissingMasterSectionPruneResult Prune(JToken section, RemovedWorldBlockPositions removedWorldBlockPositions)
        {
            var result = new MissingMasterSectionPruneResult();
            if (section is not JArray trainUnits)
            {
                Debug.Log($"trainUnits節が配列でないため列車の除去を行いません。 type={section.Type}");
                return result;
            }

            // 列車除去を先に行う。除去した列車の積荷まで空スタックとして二重に数えない
            // Remove trains first so a removed train's cargo is not counted again as emptied stacks
            RemoveTrainsWithMissingCars();
            RemoveTrainsOnRemovedBlocks();
            EmptyMissingItemsInCarContainers();
            return result;

            #region Internal

            // レールノードが1つでも解決できないと、RailPositionFactory.Restoreが位置を欠いたまま作るか、全滅でnullを返し列車が無音で消える
            // One unresolvable rail node makes RailPositionFactory.Restore build a position with gaps, or return null when none resolve so the train silently vanishes
            // 貨車と積荷ごと退避してから外し、後日の返金の入力に残す
            // Archive the train with its cars and cargo before removing it, so a later refund still has its input
            void RemoveTrainsOnRemovedBlocks()
            {
                if (removedWorldBlockPositions.IsEmpty) return;

                var removedCount = 0;
                foreach (var trainUnit in trainUnits.OfType<JObject>().ToList())
                {
                    // JSONのnullはJValueで返り?.を素通りするため、JObjectであることを型で確かめてから降りる
                    // A JSON null comes back as a JValue that slips past ?., so check for JObject before descending
                    if (trainUnit[RailPositionSaveDataKey] is not JObject railPositionSaveData || railPositionSaveData[RailSnapshotKey] is not JArray railSnapshot)
                    {
                        Debug.LogWarning($"列車のレール位置が読めないため、除去したブロックのレールに載るかを判定せず残します。 trainUnitInstanceId={trainUnit[TrainUnitInstanceIdKey]}");
                        continue;
                    }

                    if (!railSnapshot.Any(removedWorldBlockPositions.ContainsConnectionDestination)) continue;

                    Debug.LogWarning($"マスタ欠損で除去したブロックのレールに載る列車を、貨車と積荷ごとセーブから除去します。 trainUnitInstanceId={trainUnit[TrainUnitInstanceIdKey]}");
                    result.RemovedTrainUnits.Add(trainUnit.DeepClone());
                    trainUnit.Remove();
                    removedCount++;
                }

                if (0 < removedCount) Debug.LogWarning($"除去したブロックのレールに載っていた列車を除去データへ退避しました。 count={removedCount}");
            }

            // 貨車1両だけを抜くとレール上の長さと編成が食い違うので、編成ごと除去する
            // Dropping a single car would break the length on the rail against the formation, so the whole train is removed
            // 残すとTrainCar.RestoreTrainCarが「trainCarMaster is not found」を投げ、ワールドが永久に起動不能になる
            // Left in place, TrainCar.RestoreTrainCar throws "trainCarMaster is not found" and the world can never load again
            void RemoveTrainsWithMissingCars()
            {
                foreach (var trainUnit in trainUnits.OfType<JObject>().ToList())
                {
                    if (trainUnit[CarsKey] is not JArray cars) continue;

                    var missingCarGuid = cars.OfType<JObject>().Select(car => car[TrainCarMasterIdKey]?.Value<string>()).FirstOrDefault(IsMissingTrainCar);
                    if (missingCarGuid == null) continue;

                    Debug.LogWarning($"マスタに存在しない貨車を含む列車をセーブから除去します。 trainCarMasterId={missingCarGuid} trainUnitInstanceId={trainUnit[TrainUnitInstanceIdKey]}");
                    result.RemovedTrainUnits.Add(trainUnit.DeepClone());
                    trainUnit.Remove();
                }
            }

            void EmptyMissingItemsInCarContainers()
            {
                var cleaner = new MissingItemReferenceCleaner(result);
                var walker = new ItemStackPruneWalker(cleaner);
                foreach (var car in trainUnits.OfType<JObject>().SelectMany(unit => (unit[CarsKey] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()))
                {
                    var container = car[ContainerSaveDataKey];
                    if (container == null || container.Type == JTokenType.Null) continue;
                    walker.Walk(container, PrunedItemOrigin.TrainCarContainer(SaveSectionName, (long?)car[TrainCarInstanceIdKey]));
                }

                cleaner.LogRemovedItemReferences(SaveSectionName);
                walker.LogSkippedStrings(SaveSectionName);
            }

            // 読めないguidはデシリアライズで落ちる破損値。除去判定できないので理由を残して残す
            // An unreadable guid is corruption that deserialization rejects; it cannot be judged, so it stays with a logged reason
            bool IsMissingTrainCar(string guidText)
            {
                if (!Guid.TryParse(guidText, out var guid))
                {
                    Debug.LogWarning($"貨車のマスタguidが読めないため除去判定せず残します。 trainCarMasterId={guidText}");
                    return false;
                }

                return !MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(guid, out _);
            }

            #endregion
        }
    }
}
