namespace Game.Challenge.Config.TutorialParam
{
    public class MapObjectPinTutorialParam : ITutorialParam
    {
        public const string TaskCompletionType = "mapObjectPin";
        
        public readonly string MapObjectType;
        public readonly string PinText;
        
        public MapObjectPinTutorialParam(string mapObjectType, string pinText)
        {
            MapObjectType = mapObjectType;
            PinText = pinText;
        }
        
        public static ITutorialParam Create(dynamic param)
        {
            string mapObjectType = param.mapObjectType;
            string pinText = param.pinText;
            
            return new MapObjectPinTutorialParam(mapObjectType, pinText);
        }
    }
}