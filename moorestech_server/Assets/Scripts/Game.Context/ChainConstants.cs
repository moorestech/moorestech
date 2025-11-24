using System;

namespace Game.Context
{
    public static class ChainConstants
    {
        // チェーン系で共有する識別子をまとめる
        // Central identifiers for chain feature
        public const string SaveKey = "ChainPoleConnection";
        public static readonly Guid ChainItemGuid = Guid.Parse("b15c8f72-df53-4f3a-810f-55a76f65d2ce");
    }
}
