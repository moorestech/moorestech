using System.Threading;
using Client.Skit.Context;
using Client.Skit.Skit;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public partial class WaitCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var isSkip = storyContext.GetService<ISkitActionContext>().IsSkip;
            if (!isSkip)
            {
                await UniTask.Delay((int)(Seconds * 1000));
            }
            return null;
        }
    }
}
