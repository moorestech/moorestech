using System.Collections.Generic;
using System.Reflection;
using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public static class SkitCommandExecutor
    {
        public static async UniTask ExecuteAsync(
            IReadOnlyList<ICommandForgeCommand> commands, StoryContext storyContext)
        {
            var commandIndexes = BuildCommandIndexes(commands);
            var index = 0;

            // 各commandの結果だけを次の実行位置へ反映する
            // Apply each command result solely to the next execution position
            while (index < commands.Count)
            {
                var result = await commands[index].ExecuteAsync(storyContext);
                index = result == null ? index + 1 : commandIndexes[result.JumpTargetCommandId];
            }
        }

        private static Dictionary<CommandId, int> BuildCommandIndexes(
            IReadOnlyList<ICommandForgeCommand> commands)
        {
            var result = new Dictionary<CommandId, int>();
            for (var index = 0; index < commands.Count; index++)
            {
                // SourceGeneratorの共通readonly fieldを一度だけ索引化する
                // Index the SourceGenerator's shared readonly field exactly once
                var field = commands[index].GetType().GetField("CommandId", BindingFlags.Instance | BindingFlags.Public);
                result.Add((CommandId)field.GetValue(commands[index]), index);
            }
            return result;
        }
    }
}
