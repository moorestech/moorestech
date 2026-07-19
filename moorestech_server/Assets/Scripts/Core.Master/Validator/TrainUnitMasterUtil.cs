using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.TrainModule;

namespace Core.Master.Validator
{
    public static class TrainUnitMasterUtil
    {
        public static bool Validate(Train train, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += TrainCarWeightValidation();
            errorLogs += TrainCarRequiredItemsValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string TrainCarRequiredItemsValidation()
            {
                // itemGuid実在性+重複を検証
                // Validate itemGuid existence and reject duplicates within a train car's requiredItems
                var logs = "";
                foreach (var trainCar in train.TrainCars)
                {
                    if (trainCar.RequiredItems == null) continue;

                    var seenItemGuids = new HashSet<Guid>();
                    foreach (var requiredItem in trainCar.RequiredItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(requiredItem.ItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[TrainUnitMaster] TrainCar:{trainCar.TrainCarGuid} has invalid RequiredItem.ItemGuid:{requiredItem.ItemGuid}\n";
                        }

                        // ConstructionCostServiceは重複を合算しないため、重複定義はマスタエラーとする
                        // ConstructionCostService does not sum duplicates, so a duplicate definition is a master error
                        if (!seenItemGuids.Add(requiredItem.ItemGuid))
                        {
                            logs += $"[TrainUnitMaster] TrainCar:{trainCar.TrainCarGuid} has duplicate RequiredItem.ItemGuid:{requiredItem.ItemGuid}\n";
                        }

                        // count 0以下は無償設置と0個返却を生むためマスタエラー
                        // Non-positive counts allow free placement and zero-stack refunds, so treat them as master errors
                        if (requiredItem.Count <= 0)
                        {
                            logs += $"[TrainUnitMaster] TrainCar:{trainCar.TrainCarGuid} has invalid RequiredItem.Count:{requiredItem.Count}\n";
                        }
                    }
                }

                return logs;
            }

            string TrainCarWeightValidation()
            {
                var logs = "";
                foreach (var trainCar in train.TrainCars)
                {
                    if (trainCar.Weight <= 0)
                    {
                        logs += $"[TrainUnitMaster] TrainCar:{trainCar.TrainCarGuid} has invalid Weight:{trainCar.Weight}\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(
            Train train,
            out Dictionary<Guid, TrainCarMasterElement> trainCarMastersByGuid)
        {
            // Dictionary構築（Validate成功後に実行）
            // Build dictionaries (executed after Validate succeeds)
            trainCarMastersByGuid = train.TrainCars.ToDictionary(car => car.TrainCarGuid, car => car);
        }
    }
}
