#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class JumpCommand : ICommandForgeCommand
    {
        public const string Type = "jump";
        public readonly CommandId CommandId;
        
        public readonly string TargetLabel;
        
  
        public static JumpCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var TargetLabel = (string)json["targetLabel"];
            
            
            return new JumpCommand(commandId, TargetLabel);
        }
        
        public JumpCommand(int commandId, string TargetLabel)
        {
            CommandId = (CommandId)commandId;
            
        this.TargetLabel = TargetLabel;
        
        }
    }
}
#endif