using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public interface IEnvironmentRoot
    {
        void SetActive(bool enable);
    }
    
    public interface IBlockObjectControl
    {
        void SetActive(bool enable);
    }
    
    public partial class InGameObjectControlCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.GetService<IEnvironmentRoot>().SetActive(BackgroundEnable);
            storyContext.GetService<IBlockObjectControl>().SetActive(BlockEnable);
            return null;
        }
    }
}