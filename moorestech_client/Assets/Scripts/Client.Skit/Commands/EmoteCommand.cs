using Cysharp.Threading.Tasks;
using static System.Enum;

namespace CommandForgeGenerator.Command
{
    public partial class EmoteCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var character = storyContext.GetCharacter(Character);
            var emotionType = (EmotionType)Parse(typeof(EmotionType), Emotion);
            character.SetEmotion(emotionType, 0f);
            return null;
        }
    }
}
