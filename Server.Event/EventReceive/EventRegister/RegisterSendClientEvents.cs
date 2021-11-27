using World.Event;

namespace Server.Event.EventReceive.EventRegister
{
    //必要なイベントのインスタンスをまとめて生成する
    //インスタンスは生成させたら自動で各種イベントに登録される
    public class RegisterSendClientEvents
    {
        public RegisterSendClientEvents(BlockPlaceEvent blockPlaceEvent,EventProtocolProvider eventProtocolProvider)
        {
            new ReceivePlaceBlockEvent(blockPlaceEvent,eventProtocolProvider);
            new ReceiveInventoryUpdateEvent();
        }
    }
}