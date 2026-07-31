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

    public interface ISkitMapObjectControl
    {
        void SetActive(bool enable);
    }

    public interface ISkitEntityObjectControl
    {
        void SetActive(bool enable);
    }

    public partial class InGameObjectControlCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            storyContext.GetService<ISkitEnvironmentRoot>().SetActive(BackgroundEnable);
            storyContext.GetService<ISkitBlockObjectControl>().SetActive(BlockEnable);

            // mapObjectとエンティティはEnvironment外に生成されるため個別に消す
            // Map objects and entities live outside Environment, so hide them individually
            storyContext.GetService<ISkitMapObjectControl>().SetActive(MapObjectEnable);
            storyContext.GetService<ISkitEntityObjectControl>().SetActive(EntityEnable);
            return null;
        }
    }
}
