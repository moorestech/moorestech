using System;
using Game.Block.Interface.Component;
using UnitGenerator;

namespace Game.CraftChainer.CraftNetwork
{
    /// <summary>
    /// CraftChainerネットワークを構成するブロックであることを示すためのインターフェース
    /// IDを永続化する必要があるため、IBlockSaveStateを継承している
    ///
    /// An interface to indicate that it is a block that makes up the CraftChainer network.
    /// Inherits IBlockSaveState because the ID needs to be persisted
    /// </summary>
    public interface ICraftChainerNode : IBlockSaveState 
    {
        public CraftChainerNodeId NodeId { get; }
    }
    
    [UnitOf(typeof(int))]
    public partial struct CraftChainerNodeId
    {
        public static CraftChainerNodeId Invalid => new(-1); 
            
        private static readonly Random _random = new();
        
        public static CraftChainerNodeId Create()
        {
            var id = 0; // 0番はデフォルトターゲットとして扱うため予約
            while (id == Invalid.value)
            {
                id = _random.Next(int.MinValue, int.MaxValue);
            }
            
            return new CraftChainerNodeId(id);
        } 
    }
    
    public enum CraftChainerNodeType
    {
        CenterConsole,
        Transporter,
        
        CraftFactory,
        
        Storage,
    }
}