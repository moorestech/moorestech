namespace Game.Challenge.Config.TutorialParam
{
    public class KeyControlTutorialParam : ITutorialParam
    {
        public const string TaskCompletionType = "keyControl";
        
        public readonly string UiState;
        public readonly string ControlText;
        
        public KeyControlTutorialParam(string uiState, string controlText)
        {
            UiState = uiState;
            ControlText = controlText;
        }
        
        public static ITutorialParam Create(dynamic param)
        {
            string uiState = param.uiState;
            string controlText = param.controlText;
            
            return new KeyControlTutorialParam(uiState, controlText);
        }
    }
}