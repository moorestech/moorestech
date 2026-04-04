using System;
using Game.Fluid;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Mooresmaster.Model.TrainModule;
using FluidContainer = Game.Fluid.FluidContainer;

namespace Tests.Util
{
    // テスト用の貨車生成ロジックをまとめて扱いやすくする
    // Centralizes train-car creation helpers for reuse across tests.
    public static class TrainTestCarFactory
    {
        public static readonly Guid TestFuelItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        public const double TestFuelDuration = 999999;

        // 任意値でTrainCarを生成する基本メソッド
        // Creates a train car from explicit parameters and optional GUIDs.
        public static (TrainCar trainCar, ItemTrainCarContainer itemContainer) CreateTrainCarWithItemContainer(
            int masterId,
            Guid trainCarGuid,
            Guid itemGuid,
            int tractionForce,
            int inventorySlotCount,
            int length,
            bool isFacingForward)
        {
            var element = CreateMasterElement(masterId, trainCarGuid, itemGuid, tractionForce, inventorySlotCount, length);
            var itemContainer = ItemTrainCarContainer.CreateWithEmptySlots(inventorySlotCount);
            var trainCar = new TrainCar(element, isFacingForward);
            trainCar.SetContainer(itemContainer);

            if (tractionForce > 0) trainCar.SetRemainFuelTime(TestFuelDuration);

            return (trainCar, itemContainer);
        }

        // GUIDを意識せずにTrainCarを生成する簡易メソッド
        // Convenience overload for callers that do not care about GUIDs.
        public static (TrainCar trainCar, ItemTrainCarContainer itemContainer) CreateTrainCarWithItemContainer(
            int masterId,
            int tractionForce,
            int inventorySlotCount,
            int length,
            bool isFacingForward)
        {
            return CreateTrainCarWithItemContainer(masterId, Guid.Empty, Guid.Empty, tractionForce, inventorySlotCount, length, isFacingForward);
        }

        // TrainCarMasterElementだけを取得するヘルパー
        // Returns a reusable master element without wrapping it in a TrainCar.
        public static TrainCarMasterElement CreateMasterElement(
            int masterId,
            int tractionForce,
            int inventorySlotCount,
            int length)
        {
            return CreateMasterElement(masterId, Guid.Empty, Guid.Empty, tractionForce, inventorySlotCount, length);
        }

        // GUID指定付きのTrainCarMasterElementを生成
        // Allows tests to control GUIDs when building master elements.
        public static TrainCarMasterElement CreateMasterElement(
            int masterId,
            Guid trainCarGuid,
            Guid itemGuid,
            int tractionForce,
            int inventorySlotCount,
            int length)
        {
            var fuelItems = tractionForce > 0
                ? new[] { new TrainFuelItemsElement(0, TestFuelItemGuid, (float)TestFuelDuration) }
                : null;
            return new TrainCarMasterElement(masterId, trainCarGuid, itemGuid, null, 100, tractionForce, inventorySlotCount, length, fuelItems, null);
        }

        public static (TrainCar trainCar, FluidTrainCarContainer fluidContainer) CreateTrainCarWithFluidContainer(
            int masterId,
            int tractionForce,
            double fluidCapacity,
            int length,
            bool isFacingForward)
        {
            var element = CreateMasterElement(masterId, Guid.Empty, Guid.Empty, tractionForce, 0, length);
            var fluidContainer = new FluidTrainCarContainer(new FluidContainer(fluidCapacity));
            var trainCar = new TrainCar(element, isFacingForward);
            trainCar.SetContainer(fluidContainer);

            if (tractionForce > 0) trainCar.SetRemainFuelTime(TestFuelDuration);

            return (trainCar, fluidContainer);
        }

        // ランダム値でのTrainCarMasterElement生成をサポート
        // Supports randomized car parameters while keeping tests deterministic.
        public static TrainCarMasterElement CreateRandomMasterElement(
            int masterId,
            Random random,
            RandomTrainCarParameters parameters)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var traction = SampleInRange(random, parameters.MinTractionForce, parameters.MaxTractionForce);
            var slots = SampleInRange(random, parameters.MinInventorySlotCount, parameters.MaxInventorySlotCount);
            var length = SampleInRange(random, parameters.MinLength, parameters.MaxLength);
            return CreateMasterElement(masterId, Guid.Empty, Guid.Empty, traction, slots, length);
        }

        // ランダム値でそのままTrainCarを生成
        // Builds a TrainCar directly from randomized parameters.
        public static TrainCar CreateRandomTrainCar(
            int masterId,
            Random random,
            RandomTrainCarParameters parameters,
            bool isFacingForward)
        {
            var element = CreateRandomMasterElement(masterId, random, parameters);
            return new TrainCar(element, isFacingForward);
        }

        private static int SampleInRange(Random random, int min, int max)
        {
            var low = Math.Min(min, max);
            var high = Math.Max(min, max);
            return high == low ? low : random.Next(low, high + 1);
        }

        public readonly struct RandomTrainCarParameters
        {
            public RandomTrainCarParameters(
                int minTractionForce,
                int maxTractionForce,
                int minInventorySlotCount,
                int maxInventorySlotCount,
                int minLength,
                int maxLength)
            {
                MinTractionForce = minTractionForce;
                MaxTractionForce = maxTractionForce;
                MinInventorySlotCount = minInventorySlotCount;
                MaxInventorySlotCount = maxInventorySlotCount;
                MinLength = minLength;
                MaxLength = maxLength;
            }

            public int MinTractionForce { get; }
            public int MaxTractionForce { get; }
            public int MinInventorySlotCount { get; }
            public int MaxInventorySlotCount { get; }
            public int MinLength { get; }
            public int MaxLength { get; }
        }
    }
}
