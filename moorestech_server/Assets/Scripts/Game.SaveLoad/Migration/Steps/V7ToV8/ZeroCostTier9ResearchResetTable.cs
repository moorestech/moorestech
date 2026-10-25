using System;
using System.Collections.Generic;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// 旧マスタでコスト0のまま完了できたTier9研究と、旧マスタでその研究が解放していたアイテム・ブロック
    /// Tier9 research that old masters let players finish at zero cost, and the items/blocks each of them unlocked there
    /// </summary>
    /// <remarks>
    /// ステップはマスタを引かないのでGUIDを直に持つ。値は moorestech_master の server_v8 research.json の履歴（cdc6abd〜19ef0c2）の和集合
    /// Steps never read the master, so GUIDs are held directly; values are the union over the history of moorestech_master server_v8 research.json (cdc6abd to 19ef0c2)
    /// </remarks>
    public static class ZeroCostTier9ResearchResetTable
    {
        // 辞書より先に初期化する（静的初期化は記述順）
        // Initialize before the dictionary (static initializers run in textual order)
        private static readonly Guid[] Nothing = Array.Empty<Guid>();

        public static readonly IReadOnlyDictionary<Guid, ResearchUnlockGuids> UnlocksByResearch = new Dictionary<Guid, ResearchUnlockGuids>
        {
            // フィルター分岐器・機械オーバークロック: 旧マスタでは何も解放しない
            // Filter splitter and machine overclock: unlocked nothing in old masters
            [Guid.Parse("019e3aab-32c6-7166-a184-d761153c2498")] = new ResearchUnlockGuids(Nothing, Nothing),
            [Guid.Parse("019e3ae2-5005-7439-b242-9b7b3191a2c3")] = new ResearchUnlockGuids(Nothing, Nothing),
            // 金属超精密加工: 金属3Dプリンタのレシピ表示
            // Metal precision machining: the metal 3D printer recipe view
            [Guid.Parse("019e3af5-f2e6-76de-a80f-5e66e79fa35d")] = new ResearchUnlockGuids(new[] { Guid.Parse("019e3af5-e750-71ae-ab09-2b98f3db9913") }, Nothing),
            // 核融合炉: 核融合炉ブロック
            // Fusion reactor: the fusion reactor block
            [Guid.Parse("019e3af0-297f-726e-8a02-0a8518124e6d")] = new ResearchUnlockGuids(Nothing, new[] { Guid.Parse("01a162f1-ab04-76e2-94cf-dc713edeb63b") }),
            // 反物質爆弾: 反物質爆弾のレシピ表示
            // Antimatter bomb: the antimatter bomb recipe view
            [Guid.Parse("019e3aef-a149-746b-8fad-159e0434cb51")] = new ResearchUnlockGuids(new[] { Guid.Parse("01a162ab-07ba-73c1-9dd6-38e1e867663b") }, Nothing),
            // 軌道エレベーターの建設: グラフェン系とロケット素材のレシピ表示、宇宙エレベーター拠点とロケット発射台
            // Orbital elevator construction: graphene and rocket material recipe views, the space elevator base and the rocket launch pad
            [Guid.Parse("01a16291-4094-7369-a87f-5dfe2197d25e")] = new ResearchUnlockGuids(
                new[]
                {
                    Guid.Parse("01a16290-f793-748b-a0b0-91d44be0d4ab"),
                    Guid.Parse("01a16297-4724-73b9-82c3-2e77120be77d"),
                    Guid.Parse("01a16297-6665-765d-8e19-96d357394c3e"),
                    Guid.Parse("01a16297-a7f5-759b-91ba-be4021ca3bf2"),
                    Guid.Parse("01a162a4-9334-7286-a8ba-7daf9c0f407a"),
                    Guid.Parse("01a162a8-e435-73d0-bddb-08533819b80e"),
                },
                new[]
                {
                    Guid.Parse("01a162aa-cec0-738c-93c5-f58adc87fa7f"),
                    Guid.Parse("01a162b0-7769-7419-af24-bb35528b93d2"),
                }),
        };
    }

    // 1研究が旧マスタで解放したアイテム（レシピ表示）とブロック
    // The items (recipe views) and blocks one research unlocked in old masters
    public sealed class ResearchUnlockGuids
    {
        public readonly IReadOnlyList<Guid> ItemGuids;
        public readonly IReadOnlyList<Guid> BlockGuids;

        public ResearchUnlockGuids(IReadOnlyList<Guid> itemGuids, IReadOnlyList<Guid> blockGuids)
        {
            ItemGuids = itemGuids;
            BlockGuids = blockGuids;
        }
    }
}
