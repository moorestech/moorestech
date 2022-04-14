namespace TestNameSpace
{
    public class StartLocalGame
    {
        
#if UNITY_EDITOR_WIN
        private const string ServerExePath = "./Server/Server.exe";
#elif UNITY_EDITOR_OSX
        private const string ServerExePath = "";
#elif UNITY_EDITOR_OSX
        private const string ServerExePath = "";
#endif
        private void A()
        {

        }
    }
}