using System;
using System.Collections.Generic;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Map.Interface.MapObject;
using Mooresmaster.Model.MapModule;
using UnityEngine;
using Random = System.Random;

namespace Game.Map
{
    /// <summary>
    ///     木や小石など基本的に動かないマップオブジェクト
    /// </summary>
    public class VanillaStaticMapObject : IMapObject
    {
        public int InstanceId { get; }
        public Guid MapObjectGuid { get; }
        public bool IsDestroyed { get; private set; }
        public bool IsDecoration => MapObjectMaster.IsDecoration(_mapObjectConfig);
        public Vector3 Position { get; }
        public int CurrentHp { get; private set; }

        public event Action OnDestroy;

        private readonly MapObjectMasterElement _mapObjectConfig;
        // 装飾物では採掘設定が存在しない。Attackの装飾物ガードを抜けた先でだけ参照する
        // Mining settings are absent for a decoration; referenced only past the decoration guard in Attack
        private readonly IMinableMapObjectParam _minableParam;
        private readonly Random _random;

        public VanillaStaticMapObject(int instanceId, Guid mapObjectGuid, bool isDestroyed, int currentHp, Vector3 position)
        {
            // マップオブジェクトの設定を取得する
            // Retrieve the map object configuration
            _mapObjectConfig = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObjectGuid);
            InstanceId = instanceId;
            MapObjectGuid = mapObjectGuid;
            IsDestroyed = isDestroyed;
            Position = position;
            CurrentHp = currentHp;

            // 採掘設定と乱数生成器を準備する
            // Prepare the mining settings and the random generator
            _minableParam = _mapObjectConfig.MiningParam as IMinableMapObjectParam;
            _random = new Random();
        }
        
        public List<IItemStack> Attack(int damage)
        {
            // 装飾物はHP境界も取得物も持たないため、どの経路から殴られても成立しない
            // A decoration has neither HP thresholds nor drops, so a hit from any path does nothing
            if (IsDecoration) return new List<IItemStack>();

            // 与えられたダメージを適用して破壊状態を判定する
            // Apply incoming damage and determine the destroyed state
            var lastHp = CurrentHp;
            CurrentHp -= damage;
            if (CurrentHp <= 0) Destroy();

            // 与ダメージで越えたHP境界数を算出する
            // Calculate how many HP thresholds were crossed by damage
            var earnedCount = CalculateEarnedCount(lastHp, CurrentHp);
            if (earnedCount == 0) return new List<IItemStack>();

            // 越えた回数に応じてランダムな数量でアイテムを生成する
            // Create reward items with random quantity based on the crossed threshold count
            var earnedItems = new List<IItemStack>();
            for (var i = 0; i < earnedCount; i++)
            {
                GenerateEarnItems(earnedItems);
            }

            return earnedItems;

            #region Internal

            int CalculateEarnedCount(int beforeHp, int afterHp)
            {
                // 破壊済みオブジェクトでは付与処理を行わない
                // Skip reward calculation for already destroyed objects
                if (beforeHp <= 0) return 0;

                // 直前HPに基づく開始境界を設定する
                // Determine the starting threshold based on the previous HP
                var earnItemHpInterval = _minableParam.EarnItemHpInterval;
                var threshold = ((beforeHp - 1) / earnItemHpInterval) * earnItemHpInterval;
                var crossedCount = 0;

                while (threshold >= 0 && afterHp <= threshold)
                {
                    crossedCount++;
                    threshold -= earnItemHpInterval;
                }

                return crossedCount;
            }

            void GenerateEarnItems(List<IItemStack> items)
            {
                // 設定に基づいて毎回ランダムな数量のアイテムを生成
                // Generate items with random quantity based on configuration each time
                foreach (var earnItemConfig in _minableParam.EarnItems.items)
                {
                    var itemCount = _random.Next(earnItemConfig.MinCount, earnItemConfig.MaxCount + 1);
                    var itemId = MasterHolder.ItemMaster.GetItemId(earnItemConfig.ItemGuid);
                    items.AddRange(ServerContext.ItemStackFactory.CreateSplitStacks(itemId, itemCount));
                }
            }

            #endregion
        }
        
        public void Destroy()
        {
            CurrentHp = 0;
            IsDestroyed = true;
            OnDestroy?.Invoke();
        }
    }
}
