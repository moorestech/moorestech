using System;
using UnityEngine;

namespace Game.Map.Interface
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
        ///     そのオブジェクトの種類 <see cref="VanillaMapObjectType" /> などを参照
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
        public int Hp { get; }

        /// <summary>
        ///     獲得したとき入手できるアイテム
        /// </summary>
        public int ItemId { get; }

        int ItemCount { get; }


        /// <summary>
        /// HPを減らして、HPが0以下になったらtrueを返す
        /// 0以下になったらDestroyをする
        /// </summary>
        public bool Attack(int damage);

        /// <summary>
        /// オブジェクトを破壊する
        /// </summary>
        public void Destroy();

        public event Action OnDestroy;
    }
}