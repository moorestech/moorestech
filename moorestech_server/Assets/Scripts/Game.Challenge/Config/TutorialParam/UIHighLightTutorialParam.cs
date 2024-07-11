namespace Game.Challenge.Config.TutorialParam
{
    public class UIHighLightTutorialParam : ITutorialParam
    {
        public const string TaskCompletionType = "uiHighLight";
        
        public readonly string UiState;
        public readonly string HighLightUI;
        
        public UIHighLightTutorialParam(string uiState, string highLightUI)
        {
            UiState = uiState;
            HighLightUI = highLightUI;
        }
        
        public static ITutorialParam Create(dynamic param)
        {
            string uiState = param.uiState;
            string highLightUI = param.highLightUI;
            
            return new UIHighLightTutorialParam(uiState, highLightUI);
        }
    }
}