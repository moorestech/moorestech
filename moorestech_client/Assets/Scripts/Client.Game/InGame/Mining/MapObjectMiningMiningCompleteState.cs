using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.SoundEffect;
using Client.Input;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.MapObjectsModule;
using UnityEngine;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningMiningCompleteState : IMapObjectMiningState
    {
        private readonly MapObjectGameObject _completedMapObjectGameObject;
        private readonly int _attackDamage;
        
        public MapObjectMiningMiningCompleteState(MapObjectGameObject completedMapObjectGameObject, int attackDamage)
        {
            _completedMapObjectGameObject = completedMapObjectGameObject;
            _attackDamage = attackDamage;
        }
        
        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            var masterElement = _completedMapObjectGameObject.MapObjectMasterElement;
            
            PlaySoundEffect(masterElement);
            
            var instanceId = _completedMapObjectGameObject.InstanceId;
            ClientContext.VanillaApi.SendOnly.AttackMapObject(instanceId, _attackDamage);
            
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