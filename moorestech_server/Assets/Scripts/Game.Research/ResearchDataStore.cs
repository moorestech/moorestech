using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.Research.Interface;

namespace Game.Research
{
    public class ResearchDataStore : IResearchDataStore
    {
        private readonly HashSet<Guid> _completed = new();
        private readonly IPlayerInventoryDataStore _inventory;
        private readonly dynamic _executor;
        private readonly ResearchEvent _event;

        public ResearchDataStore(IPlayerInventoryDataStore inventory, object executor, ResearchEvent researchEvent)
        {
            _inventory = inventory;
            _executor = executor;
            _event = researchEvent;
        }

        public bool IsResearchCompleted(Guid researchGuid) => _completed.Contains(researchGuid);

        public bool CanCompleteResearch(Guid researchGuid, int playerId)
        {
            var e = MasterHolder.ResearchMaster.GetResearchNode(researchGuid);
            if (e == null) return false;
            if (_completed.Contains(researchGuid)) return false;
            if (e.PrevResearchNodeGuid != Guid.Empty && !_completed.Contains(e.PrevResearchNodeGuid)) return false;
            return CheckRequiredItems(playerId, e);
        }

        public ResearchCompletionResult CompleteResearch(Guid researchGuid, int playerId)
        {
            if (!CanCompleteResearch(researchGuid, playerId))
            {
                return new ResearchCompletionResult { Success = false, Reason = "Research cannot be completed" };
            }

            var e = MasterHolder.ResearchMaster.GetResearchNode(researchGuid);
            if (!ConsumeRequiredItems(playerId, e))
            {
                return new ResearchCompletionResult { Success = false, Reason = "Failed to consume required items" };
            }

            _completed.Add(researchGuid);
            ExecuteActions(e.ClearedActions.items);
            _event.Invoke(playerId, researchGuid);
            return new ResearchCompletionResult { Success = true, CompletedResearchGuid = researchGuid };
        }

        public HashSet<Guid> GetCompletedResearchGuids() => new HashSet<Guid>(_completed);

        public ResearchSaveJsonObject GetSaveJsonObject()
        {
            return new ResearchSaveJsonObject
            {
                CompletedResearchGuids = _completed.Select(g => g.ToString()).ToList()
            };
        }

        public void LoadResearchData(ResearchSaveJsonObject saveData)
        {
            _completed.Clear();
            if (saveData?.CompletedResearchGuids == null) return;
            foreach (var s in saveData.CompletedResearchGuids)
            {
                if (Guid.TryParse(s, out var g))
                {
                    _completed.Add(g);
                    var e = MasterHolder.ResearchMaster.GetResearchNode(g);
                    if (e != null)
                    {
                        ExecuteUnlockActions(e.ClearedActions.items);
                    }
                }
            }
        }

        private bool CheckRequiredItems(int playerId, Mooresmaster.Model.ResearchModule.ResearchNodeMasterElement e)
        {
            var items = e.ConsumeItems;
            if (items == null || items.Length == 0) return true;
            var inv = _inventory.GetInventoryData(playerId).MainOpenableInventory;
            var required = new Dictionary<ItemId, int>();
            foreach (var ci in items)
            {
                var id = MasterHolder.ItemMaster.GetItemId(ci.ItemGuid);
                if (required.ContainsKey(id)) required[id] += ci.ItemCount; else required.Add(id, ci.ItemCount);
            }

            // Sum available counts
            var available = new Dictionary<ItemId, int>();
            for (int i = 0; i < inv.GetSlotSize(); i++)
            {
                var s = inv.GetItem(i);
                if (available.ContainsKey(s.Id)) available[s.Id] += s.Count; else available[s.Id] = s.Count;
            }
            foreach (var kv in required)
            {
                available.TryGetValue(kv.Key, out var have);
                if (have < kv.Value) return false;
            }
            return true;
        }

        private bool ConsumeRequiredItems(int playerId, Mooresmaster.Model.ResearchModule.ResearchNodeMasterElement e)
        {
            var items = e.ConsumeItems;
            if (items == null || items.Length == 0) return true;
            var inv = _inventory.GetInventoryData(playerId).MainOpenableInventory;
            // aggregate required
            var required = new Dictionary<ItemId, int>();
            foreach (var ci in items)
            {
                var id = MasterHolder.ItemMaster.GetItemId(ci.ItemGuid);
                if (required.ContainsKey(id)) required[id] += ci.ItemCount; else required.Add(id, ci.ItemCount);
            }
            // consume
            for (int slot = 0; slot < inv.GetSlotSize(); slot++)
            {
                var stack = inv.GetItem(slot);
                if (!required.TryGetValue(stack.Id, out var need) || need <= 0) continue;
                if (stack.Count <= need)
                {
                    inv.SetItem(slot, stack.SubItem(stack.Count));
                    required[stack.Id] -= stack.Count;
                }
                else
                {
                    inv.SetItem(slot, stack.SubItem(need));
                    required[stack.Id] = 0;
                }
            }
            // verify fully consumed
            foreach (var kv in required) if (kv.Value > 0) return false;
            return true;
        }

        private void ExecuteActions(object[] actions)
        {
            foreach (var a in actions) _executor.ExecuteAction(a);
        }

        private void ExecuteUnlockActions(object[] actions)
        {
            foreach (dynamic a in actions)
            {
                switch ((int)a.ChallengeActionType)
                {
                    case 0: // unlockCraftRecipe
                    case 1: // unlockItemRecipeView
                    case 2: // unlockChallengeCategory
                        _executor.ExecuteAction(a);
                        break;
                }
            }
        }
    }

    public class ResearchCompletionResult
    {
        public bool Success { get; set; }
        public Guid CompletedResearchGuid { get; set; }
        public string Reason { get; set; }
    }

    public class ResearchSaveJsonObject
    {
        public List<string> CompletedResearchGuids { get; set; }
    }
}
