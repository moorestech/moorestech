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

    // Environment外世界オブジェクトの表示窓口
    // Visibility entry point for world objects placed outside Environment, such as map objects, outcrops, and trains
    public interface ISkitWorldObjectControl
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

            // mapObject・露頭・列車・エンティティはEnvironment外に生成されるため個別に消す
            // Map objects, outcrops, trains, and entities live outside Environment, so hide them individually
            storyContext.GetService<ISkitWorldObjectControl>().SetActive(WorldObjectEnable);
            storyContext.GetService<ISkitEntityObjectControl>().SetActive(EntityEnable);
            return null;
        }
    }
}
