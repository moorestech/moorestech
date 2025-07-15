using Client.Skit.Context;
using Cysharp.Threading.Tasks;

namespace CommandForgeGenerator.Command
{
    public interface IEnvironmentRoot
    {
        void SetActive(bool enable);
    }
    
    public partial class GameEnvironmentActiveCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.GetService<IEnvironmentRoot>().SetActive(Enable);
            return null;
        }
    }
}