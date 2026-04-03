using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public interface ISkitEnvironmentRoot
    {
        void SetActive(bool enable);
    }
    
    public interface ISkitBlockObjectControl
    {
        void SetActive(bool enable);
    }
    
    public partial class InGameObjectControlCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.GetService<ISkitEnvironmentRoot>().SetActive(BackgroundEnable);
            storyContext.GetService<ISkitBlockObjectControl>().SetActive(BlockEnable);
            return null;
        }
    }
}