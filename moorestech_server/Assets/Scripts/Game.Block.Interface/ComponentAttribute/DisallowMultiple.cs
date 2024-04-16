using System;

namespace Game.Block.Interface.ComponentAttribute
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public class DisallowMultiple : Attribute
    {
    }
}