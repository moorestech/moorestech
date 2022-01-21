namespace MainGame.Network.Interface
{
    public interface ISocket
    {
        public void Send(byte[] data);
    }
}