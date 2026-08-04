using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.SoundEffect;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningMiningCompleteState : IMapObjectMiningState
    {
        private readonly MapObjectGameObject _completedMapObjectGameObject;

        public MapObjectMiningMiningCompleteState(MapObjectGameObject completedMapObjectGameObject)
        {
            _completedMapObjectGameObject = completedMapObjectGameObject;
        }

        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            var masterElement = _completedMapObjectGameObject.MapObjectMasterElement;

            PlaySoundEffect(masterElement);

            // 対象種別付きリクエストへ打撃対象を詰め、解決はサーバ権威に委ねる
            // Send a typed target request and leave resolution to the server authority
            var instanceId = _completedMapObjectGameObject.InstanceId;
            var request = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(ClientContext.PlayerConnectionSetting.PlayerId, instanceId);
            ClientContext.VanillaApi.SendOnly.SendMiningRequest(request);

            return context.CurrentFocusMapObjectGameObject == null
                ? new MapObjectMiningIdleState()
                : new MapObjectMiningFocusState();
        }
        
        
        void PlaySoundEffect(MapObjectMasterElement masterElement)
        {
            SoundEffectType soundEffectType;
            switch (masterElement.SoundEffectType)
            {
                case MapObjectMasterElement.SoundEffectTypeConst.stone:
                    soundEffectType = SoundEffectType.DestroyStone;
                    break;
                case MapObjectMasterElement.SoundEffectTypeConst.tree:
                    soundEffectType = SoundEffectType.DestroyTree;
                    break;
                default:
                    soundEffectType = SoundEffectType.DestroyStone;
                    Debug.LogError("採掘音が設定されていません");
                    break;
            }
            
            SoundEffectManager.Instance.PlaySoundEffect(soundEffectType);
        }
    }
}
