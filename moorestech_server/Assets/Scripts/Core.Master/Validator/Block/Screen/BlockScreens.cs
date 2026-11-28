using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block.Screen
{
    /// <summary>
    /// ブロック画面IDの選択肢と「開けるか」の判定を1か所に持つ
    /// Holds the block screen ID options and the "is openable" rule in one place
    /// </summary>
    public static class BlockScreens
    {
        // 選択肢の正はスキーマから生成された定数クラスだけ。ここで一覧を書き写さない
        // The generated const class is the only source of options; never copy the list here
        private static readonly HashSet<string> KnownScreens = typeof(BlockMasterElement.BlockScreenConst)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue())
            .ToHashSet();

        internal static bool IsKnownScreen(string blockScreen)
        {
            return KnownScreens.Contains(blockScreen);
        }

        public static bool IsOpenable(BlockMasterElement block)
        {
            return block.BlockScreen != BlockMasterElement.BlockScreenConst.None;
        }
    }
}
