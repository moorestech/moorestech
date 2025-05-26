#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class WaitCommand : ICommandForgeCommand
    {
        public const string Type = "wait";
        public readonly CommandId CommandId;
        
        public readonly float Seconds;
        
  
        public static WaitCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Seconds = (float)json["seconds"];
            
            
            return new WaitCommand(commandId, Seconds);
        }
        
        public WaitCommand(int commandId, float Seconds)
        {
            CommandId = (CommandId)commandId;
            
        this.Seconds = Seconds;
        
        }
    }
}
#endif