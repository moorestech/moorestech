using System.Collections.Generic;
using Game.Block.Factory.BlockTemplate;

namespace Server.Core.Block
{
    public interface IBlockTemplates
    {
        Dictionary<string, IBlockTemplate> BlockTypesDictionary { get; }
    }
}