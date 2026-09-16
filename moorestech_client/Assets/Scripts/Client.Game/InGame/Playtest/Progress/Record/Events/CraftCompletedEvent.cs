using System;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // サーバーで素材を消費し終えたクラフト。素材不足で拒否された要求は載らない
    // A craft the server finished consuming materials for; a request rejected for missing materials never lands here
    internal sealed class CraftCompletedEvent : IProgressEvent
    {
        public const string TypeName = "craftCompleted";
        private const string RecipeGuidKey = "recipeGuid";

        public string T { get; }
        public ulong Tick { get; }
        public string RecipeGuid { get; }

        public CraftCompletedEvent(DateTime utc, ulong tick, string recipeGuid) : this(ProgressUtcTime.ToIso(utc), tick, recipeGuid)
        {
        }

        private CraftCompletedEvent(string t, ulong tick, string recipeGuid)
        {
            T = t;
            Tick = tick;
            RecipeGuid = recipeGuid;
        }

        public static CraftCompletedEvent FromData(string t, ulong tick, JObject data)
        {
            return ProgressEventLine.TryReadString(data, RecipeGuidKey, TypeName, out var recipeGuid) ? new CraftCompletedEvent(t, tick, recipeGuid) : null;
        }

        public void ApplyTo(ProgressRecordAggregate aggregate)
        {
            aggregate.CountCraft();
        }

        public JObject ToJson()
        {
            return ProgressEventLine.Envelope(this, TypeName, new JObject { [RecipeGuidKey] = RecipeGuid });
        }
    }
}
