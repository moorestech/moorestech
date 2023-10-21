using System;
using Game.Base;

namespace Game.MapObject.Interface
{
    /// <summary>
    ///     。
    /// </summary>
    public interface IMapObject
    {

        ///     ID
        ///     

        public int InstanceId { get; }


        ///      <see cref="VanillaMapObjectType" /> 

        public string Type { get; }


        ///     true

        public bool IsDestroyed { get; }


        ///     

        ServerVector3 Position { get; }


        ///     

        public int ItemId { get; }

        int ItemCount { get; }


        public void Destroy();

        public event Action OnDestroy;
    }
}