using System.Collections.Generic;
using Game.Map.Interface.Json;

namespace Game.Map.Interface
{
    // 手掘りの共有クールダウン。セーブ像に載せるためロード側と保存側から参照される
    // The shared hand-mining cooldown; both the save capture and the loader reach it through this contract
    public interface IMiningCooldownDatastore
    {
        bool IsInCooldown(int playerId, double attackSpeed);
        void RecordAttack(int playerId);
        List<PlayerMiningCooldownSaveJsonObject> GetSaveJsonObject();
        void LoadMiningCooldowns(List<PlayerMiningCooldownSaveJsonObject> saveData);
    }
}
