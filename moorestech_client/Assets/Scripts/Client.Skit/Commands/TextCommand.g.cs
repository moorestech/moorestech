#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class TextCommand : ICommandForgeCommand
    {
        public const string Type = "text";
        public readonly CommandId CommandId;
        
        public readonly string Character;
        public readonly string Body;
        
  
        public static TextCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Character = (string)json["character"];
            var Body = (string)json["body"];
            
            
            return new TextCommand(commandId, Character, Body);
        }
        
        public TextCommand(int commandId, string Character, string Body)
        {
            CommandId = (CommandId)commandId;
            
        this.Character = Character;
        this.Body = Body;
        
        }
    }
}
#endif