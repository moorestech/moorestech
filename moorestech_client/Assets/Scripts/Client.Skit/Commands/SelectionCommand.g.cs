#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class SelectionCommand : ICommandForgeCommand
    {
        public const string Type = "selection";
        public readonly CommandId CommandId;
        
        public readonly string Option1Tag;
        public readonly string Option1Label;
        public readonly string Option2Tag;
        public readonly string Option2Label;
        public readonly string Option3Tag;
        public readonly string Option3Label;
        
  
        public static SelectionCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Option1Tag = (string)json["Option1Tag"];
            var Option1Label = (string)json["Option1Label"];
            var Option2Tag = (string)json["Option2Tag"];
            var Option2Label = (string)json["Option2Label"];
            var Option3Tag = (string)json["Option3Tag"];
            var Option3Label = (string)json["Option3Label"];
            
            
            return new SelectionCommand(commandId, Option1Tag, Option1Label, Option2Tag, Option2Label, Option3Tag, Option3Label);
        }
        
        public SelectionCommand(int commandId, string Option1Tag, string Option1Label, string Option2Tag, string Option2Label, string Option3Tag, string Option3Label)
        {
            CommandId = (CommandId)commandId;
            
        this.Option1Tag = Option1Tag;
        this.Option1Label = Option1Label;
        this.Option2Tag = Option2Tag;
        this.Option2Label = Option2Label;
        this.Option3Tag = Option3Tag;
        this.Option3Label = Option3Label;
        
        }
    }
}
#endif