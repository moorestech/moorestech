using System;
using UnitGenerator;

namespace Game.Train.RailGraph
{
    //多分uintで十分
    [UnitOf(typeof(uint))]
    public partial struct RailNodeId
    {
        private static readonly Random Random = new();
        
        public static RailNodeId Create()
        {
            uint result = (uint)Random.Next(int.MinValue, int.MaxValue);
            return new RailNodeId(result);
        }
    }
}