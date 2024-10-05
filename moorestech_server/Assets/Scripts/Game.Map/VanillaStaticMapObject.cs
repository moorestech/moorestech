using System;
using System.Collections.Generic;
using System.Linq;
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
        
        private readonly int[] _earnItemHps;
        
        public VanillaStaticMapObject(int instanceId, Guid mapObjectGuid, bool isDestroyed, int currentHp, Vector3 position)
        {
            var mapObjectConfig = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObjectGuid);
            InstanceId = instanceId;
            MapObjectGuid = mapObjectGuid;
            IsDestroyed = isDestroyed;
            Position = position;
            CurrentHp = currentHp;
            
            
            _earnItemHps = mapObjectConfig.EarnItemHps;
            
            var random = new Random(instanceId);
            EarnItems = new List<IItemStack>();
            foreach (var earnItemConfig in mapObjectConfig.EarnItems)
            {
                var itemCount = random.Next(earnItemConfig.MinCount, earnItemConfig.MaxCount + 1);
                var itemStack = ServerContext.ItemStackFactory.Create(earnItemConfig.ItemGuid, itemCount);
                
                EarnItems.Add(itemStack);
            }
        }
        
        public List<IItemStack> Attack(int damage)
        {
            var lastHp = CurrentHp;
            CurrentHp -= damage;
            if (CurrentHp <= 0) Destroy();
            
            var earnedCount = _earnItemHps.Count(hp => lastHp > hp && CurrentHp <= hp);
            if (earnedCount == 0) return new List<IItemStack>();
            
            var earnedItems = new List<IItemStack>();
            foreach (var item in EarnItems)
            {
                var id = item.Id;
                var count = item.Count * earnedCount;
                
                earnedItems.Add(ServerContext.ItemStackFactory.Create(id, count));
            }
            
            return earnedItems;
        }
        
        public void Destroy()
        {
            CurrentHp = 0;
            IsDestroyed = true;
            OnDestroy?.Invoke();
        }
    }
}