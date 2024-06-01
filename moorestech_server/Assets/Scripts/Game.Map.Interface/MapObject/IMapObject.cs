﻿using System;
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
        public event Action OnDestroy;

        /// <summary>
        ///     マップオブジェクト自体の固有ID
        ///     オブジェクトごとに異なる
        /// </summary>
        public int InstanceId { get; }

        /// <summary>
        ///     そのオブジェクトの種類
        /// </summary>
        public string Type { get; }

        /// <summary>
        ///     オブジェクトがすでに獲得などされていればtrue
        /// </summary>
        public bool IsDestroyed { get; }

        /// <summary>
        ///     オブジェクトが存在する座標
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// MapObjectが破壊されるまでのHP
        /// </summary>
        public int CurrentHp { get; }

        /// <summary>
        ///     獲得したとき入手できるアイテム
        /// </summary>
        public List<IItemStack> EarnItems { get; }

        /// <summary>
        /// HPを減らして、入手できるアイテムを返す
        /// 0以下になったらDestroyをする
        /// </summary>
        public List<IItemStack> Attack(int damage);

        /// <summary>
        /// オブジェクトを破壊する
        /// </summary>
        public void Destroy();
    }
}