using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block.Screen
{
    /// <summary>
    /// ブロック画面IDがスキーマの選択肢にあるかを検証する（生成ローダーは enum の値を検査しないため）
    /// Validates each block screen ID is a schema option (the generated loader does not check enum values)
    /// </summary>
    internal static class BlockScreenValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = string.Empty;
            foreach (var block in blocks.Data)
            {
                if (!BlockScreens.IsKnownScreen(block.BlockScreen)) logs += $"[BlockMaster] Block:{block.Name} has unknown blockScreen:{block.BlockScreen}\n";
            }
            return logs;
        }
    }
}
