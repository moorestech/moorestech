namespace MainGame.Network.Receive
{
    public class DummyProtocol : IAnalysisPacket
    {
        public void Analysis(byte[] data) { }
    }
}