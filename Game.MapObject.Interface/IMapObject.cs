using Game.Base;

namespace Game.MapObject.Interface
{
    /// <summary>
    /// 小石や木など、マップ上にもとから配置されている静的なオブジェクトです。
    /// </summary>
    public interface IMapObject
    {
        /// <summary>
        /// マップオブジェクト自体の固有ID
        /// オブジェクトごとに異なる
        /// </summary>
        public int InstanceId { get; }
        /// <summary>
        /// そのオブジェクトの種類 <see cref="VanillaMapObjectType"/> などを参照
        /// </summary>
        public string Type { get; }
        
        /// <summary>
        /// オブジェクトがすでに獲得などされていればtrue
        /// </summary>
        public bool IsDestroyed { get; }
       
        /// <summary>
        /// オブジェクトが存在する座標
        /// </summary>
        ServerVector3 Position { get; }
        
        /// <summary>
        /// 獲得したとき入手できるアイテム
        /// </summary>
        public int ItemId { get; }


        public void Destroy();
    }
}