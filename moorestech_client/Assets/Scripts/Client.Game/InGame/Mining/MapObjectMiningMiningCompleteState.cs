using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.SoundEffect;
using Mooresmaster.Model.MapModule;
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

            // ダメージ算出はサーバ権威のため打撃対象だけを送る
            // Damage resolution is server-authoritative, so only the target is sent
            var instanceId = _completedMapObjectGameObject.InstanceId;
            ClientContext.VanillaApi.SendOnly.AttackMapObject(instanceId);

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