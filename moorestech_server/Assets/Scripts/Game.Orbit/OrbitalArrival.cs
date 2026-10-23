using Core.Master;

namespace Game.Orbit
{
    // 軌道に届いた積荷の通知。S3はこれでケーブルの累計を数える
    // Notification for cargo that reached orbit; S3 counts its cable total from it
    public class OrbitalArrival
    {
        public readonly ItemId PayloadItemId;
        public readonly int PayloadCount;

        public OrbitalArrival(ItemId payloadItemId, int payloadCount)
        {
            PayloadItemId = payloadItemId;
            PayloadCount = payloadCount;
        }
    }
}
