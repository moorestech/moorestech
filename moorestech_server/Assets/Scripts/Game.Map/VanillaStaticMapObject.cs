using System;
using Game.Map.Interface;
using UnityEngine;

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
        public int Hp { get; private set; }
        public int ItemId { get; }
        public int ItemCount { get; }

        public event Action OnDestroy;

        public VanillaStaticMapObject(int id, string type, bool isDestroyed, Vector3 position, int itemId, int itemCount)
        {
            InstanceId = id;
            Type = type;
            IsDestroyed = isDestroyed;
            Position = position;
            ItemId = itemId;
            ItemCount = itemCount;
            Hp = type switch
            {
                VanillaMapObjectType.VanillaStone => 20,
                VanillaMapObjectType.VanillaTree => 100,
                _ => 100
            };
            // TODO これは仮で100を入れている そのうちconfigから読み込むようにする
            // TODO それとHPのデータを保管していないので、それを入れるようにもする
        }


        public bool Attack(int damage)
        {
            Hp -= damage;
            if (Hp <= 0)
            {
                Destroy();
                return true;
            }

            return false;
        }

        public void Destroy()
        {
            Hp = 0;
            IsDestroyed = true;
            OnDestroy?.Invoke();
        }
    }
}