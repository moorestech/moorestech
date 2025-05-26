#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class EmoteCommand : ICommandForgeCommand
    {
        public const string Type = "emote";
        public readonly CommandId CommandId;
        
        public readonly string Character;
        public readonly string Emotion;
        
  
        public static EmoteCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Character = (string)json["character"];
            var Emotion = (string)json["emotion"];
            
            
            return new EmoteCommand(commandId, Character, Emotion);
        }
        
        public EmoteCommand(int commandId, string Character, string Emotion)
        {
            CommandId = (CommandId)commandId;
            
        this.Character = Character;
        this.Emotion = Emotion;
        
        }
    }
}
#endif