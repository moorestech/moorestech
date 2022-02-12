namespace MainGame.Network
{
    public interface ISocket
    {
        public void Send(byte[] data);
    }
}