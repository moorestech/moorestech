namespace Client.Common.Server
{
    public class NetworkConst
    {
        public const int SecondUpdateRate = 10;
        public const int UpdateIntervalMilliseconds = 1000 / SecondUpdateRate;
        public const float UpdateIntervalSeconds = 1.0f / SecondUpdateRate;
    }
}