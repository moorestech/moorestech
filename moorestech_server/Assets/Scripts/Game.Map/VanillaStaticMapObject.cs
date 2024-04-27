using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Context;
using Game.Map.Interface;
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
        public string Type { get; }
        public bool IsDestroyed { get; private set; }
        public Vector3 Position { get; }
        public int CurrentHp { get; private set; }
        public List<IItemStack> EarnItems { get; }

        public event Action OnDestroy;

        public VanillaStaticMapObject(int instanceId, string type, bool isDestroyed, int currentHp, Vector3 position)
        {
            InstanceId = instanceId;
            Type = type;
            IsDestroyed = isDestroyed;
            Position = position;
            CurrentHp = currentHp;


            var random = new Random(instanceId);
            var mapObjectConfig = ServerContext.MapObjectConfig.GetConfig(type);

            EarnItems = new List<IItemStack>();
            foreach (var earnItemConfig in mapObjectConfig.EarnItems)
            {
                var itemCount = random.Next(earnItemConfig.MinCount, earnItemConfig.MaxCount + 1);
                var itemStack = ServerContext.ItemStackFactory.Create(earnItemConfig.ItemId, itemCount);

                EarnItems.Add(itemStack);
            }
        }


        public bool Attack(int damage)
        {
            CurrentHp -= damage;
            if (CurrentHp <= 0)
            {
                Destroy();
                return true;
            }

            return false;
        }

        public void Destroy()
        {
            CurrentHp = 0;
            IsDestroyed = true;
            OnDestroy?.Invoke();
        }
    }
}