namespace Server.Event
{
    public class RegisterSendClientEvents
    {
        public void Init()
        {
            ReceivePlaceBlockEvent.Init();
        }
        private static RegisterSendClientEvents _instance;
        public static RegisterSendClientEvents Instance
        {
            get
            {
                if (_instance is null) _instance = new RegisterSendClientEvents();
                return _instance;
            }
        }
    }
}