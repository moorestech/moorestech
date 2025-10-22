using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Map.Interface.MapObject;
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
        public Vector3 Position { get; }
        public int CurrentHp { get; private set; }
        
        public List<IItemStack> EarnItems { get; }
        
        public event Action OnDestroy;
        
        private readonly int _earnItemHpInterval;
        
        public VanillaStaticMapObject(int instanceId, Guid mapObjectGuid, bool isDestroyed, int currentHp, Vector3 position)
        {
            // マップオブジェクトの設定を取得する
            // Retrieve the map object configuration
            var mapObjectConfig = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObjectGuid);
            InstanceId = instanceId;
            MapObjectGuid = mapObjectGuid;
            IsDestroyed = isDestroyed;
            Position = position;
            CurrentHp = currentHp;
            
            // アイテム付与間隔と乱数生成器を準備する
            // Prepare the item reward interval and deterministic random
            _earnItemHpInterval = mapObjectConfig.EarnItemHpInterval;
            var random = new Random(instanceId);
            EarnItems = new List<IItemStack>();

            // コンフィグに沿って初期獲得アイテムを生成する
            // Create initial earnable items according to configuration
            foreach (var earnItemConfig in mapObjectConfig.EarnItems)
            {
                var itemCount = random.Next(earnItemConfig.MinCount, earnItemConfig.MaxCount + 1);
                var itemStack = ServerContext.ItemStackFactory.Create(earnItemConfig.ItemGuid, itemCount);
                
                EarnItems.Add(itemStack);
            }
        }
        
        public List<IItemStack> Attack(int damage)
        {
            // 与えられたダメージを適用して破壊状態を判定する
            // Apply incoming damage and determine the destroyed state
            var lastHp = CurrentHp;
            CurrentHp -= damage;
            if (CurrentHp <= 0) Destroy();
            
            // 与ダメージで越えたHP境界数を算出する
            // Calculate how many HP thresholds were crossed by damage
            var earnedCount = CalculateEarnedCount(lastHp, CurrentHp);
            if (earnedCount == 0) return new List<IItemStack>();
            
            // 越えた回数に応じて付与アイテムを生成する
            // Create reward items based on the crossed threshold count
            var earnedItems = new List<IItemStack>();
            foreach (var item in EarnItems)
            {
                var id = item.Id;
                var count = item.Count * earnedCount;
                var maxStack = MasterHolder.ItemMaster.GetItemMaster(id).MaxStack;
                
                // 最大のアイテムスタック数のアイテムを追加する
                var fullItemCount = count / maxStack;
                for (int i = 0; i < fullItemCount; i++)
                {
                    earnedItems.Add(ServerContext.ItemStackFactory.Create(id, maxStack));
                }
                
                // あまりを追加する
                var remainCount = count % maxStack;
                if (remainCount != 0)
                {
                    earnedItems.Add(ServerContext.ItemStackFactory.Create(id, remainCount));
                }
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
                var threshold = ((beforeHp - 1) / _earnItemHpInterval) * _earnItemHpInterval;
                var crossedCount = 0;

                while (threshold >= 0 && afterHp <= threshold)
                {
                    crossedCount++;
                    threshold -= _earnItemHpInterval;
                }

                return crossedCount;
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
