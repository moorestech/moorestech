using System;
using System.Collections.Generic;
using Core.Item.Interface;
using UnityEngine;

namespace Game.Map.Interface.MapObject
{
    /// <summary>
    ///     小石や木など、マップ上にもとから配置されている静的なオブジェクトです。
    /// </summary>
    public interface IMapObject
    {
        /// <summary>
        ///     マップオブジェクト自体の固有ID
        ///     オブジェクトごとに異なる
        /// </summary>
        public int InstanceId { get; }
        
        /// <summary>
        ///     そのオブジェクトのID
        /// </summary>
        public Guid MapObjectGuid { get; }
        
        /// <summary>
        ///     オブジェクトがすでに獲得などされていればtrue
        /// </summary>
        public bool IsDestroyed { get; }
        
        /// <summary>
        ///     オブジェクトが存在する座標
        /// </summary>
        Vector3 Position { get; }
        
        /// <summary>
        ///     MapObjectが破壊されるまでのHP
        /// </summary>
        public int CurrentHp { get; }

        public event Action OnDestroy;
        
        /// <summary>
        ///     HPを減らして、入手できるアイテムを返す
        ///     0以下になったらDestroyをする
        /// </summary>
        public List<IItemStack> Attack(int damage);
        
        /// <summary>
        ///     オブジェクトを破壊する
        /// </summary>
        public void Destroy();
    }
}