using Game.World.Interface.DataStore;

namespace Server.Boot.Loop.PacketProcessing
{
    public class WorldMutationTickEndUpdater
    {
        private readonly TickEndPacketQueue _packetQueue;
        private readonly IBlockRemovalReservationService _blockRemovalReservationService;

        public WorldMutationTickEndUpdater(
            TickEndPacketQueue packetQueue,
            IBlockRemovalReservationService blockRemovalReservationService)
        {
            _packetQueue = packetQueue;
            _blockRemovalReservationService = blockRemovalReservationService;
        }

        public void Update()
        {
            // tick末尾開始時の入力を固定してから過負荷破断を優先する
            // Freeze tick-end input first, then prioritize overload breakage
            _packetQueue.FreezeCurrentPackets();
            _blockRemovalReservationService.ApplyReservedRemovals();

            // 破断後の確定済み世界へ固定パケットをFIFO適用する
            // Apply frozen packets in FIFO order to the post-breakage world
            _packetQueue.ProcessFrozenPackets();
        }
    }
}
