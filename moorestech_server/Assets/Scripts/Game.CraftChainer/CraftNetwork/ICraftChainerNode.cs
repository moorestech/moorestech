using System;
using Game.Block.Interface.Component;
using UnitGenerator;

namespace Game.CraftChainer.CraftNetwork
{
    public interface ICraftChainerNode : IBlockComponent
    {
        public CraftChainerNodeId NodeId { get; }
    }
    
    [UnitOf(typeof(int))]
    public partial struct CraftChainerNodeId
    {
        private static readonly Random _random = new();
        
        public static CraftChainerNodeId Create()
        {
            var id = 0; // 0番はデフォルトターゲットとして扱うため予約
            while (id == 0)
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