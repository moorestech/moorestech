using System;
using Core.Master;
using Game.MapGeneration.Identity;

namespace Game.MapGeneration.Transfer
{
    /// <summary>
    ///     転送メタが指すワールドを、いま動いているビルドの生成システムが再現できるかを検査する。
    ///     値だけで完結する検査(解像度・原点・チャンク数)と違い、MasterHolderとビルド定数を読むのでTerrainTransferMetaから分けてある
    ///     Checks whether the generation system of the running build can reproduce the world a transfer meta points at.
    ///     Unlike the checks closed over the meta's own values (resolution, origins, chunk count), these read MasterHolder and build constants, so they live apart from TerrainTransferMeta
    /// </summary>
    // 指紋の算出がPipelineまで降りるため、クライアント可視のGame.MapGeneration.Contractには入れられずcore側のアセンブリに置く
    // Computing the fingerprint descends into Pipeline, so this cannot sit in the client-visible Game.MapGeneration.Contract and stays in the core assembly
    public static class TerrainTransferMetaCompatibility
    {
        // 指紋の算出も照合もこのメソッドだけが持つ。呼び出し元にMasterHolderから組み直させると条件変更(templateも持つ等)の直し忘れが起きる
        // This method alone computes and matches the fingerprint; rebuilding it from MasterHolder at each caller would risk a missed update when the condition changes (e.g. templates gaining one)
        public static void ThrowIfGenerationMasterDiffers(
            this GeneratedTerrainTransferPayload generatedPayload, string serverDataDirectory)
        {
            var currentFingerprint = generatedPayload.ComputeCurrentGenerationMasterFingerprint(serverDataDirectory);
            if (currentFingerprint == generatedPayload.GenerationMasterFingerprint) return;
            throw new InvalidOperationException(
                $"Generation master fingerprint '{currentFingerprint}' differs from the world's '{generatedPayload.GenerationMasterFingerprint}'. " +
                "Delete the world directory and generate the world again.");
        }

        // 現在の生成マスタの指紋。照合にも、ワールド側の記録を現在値へ進めるときの新しい値にも使う唯一の算出
        // The current generation master's fingerprint: the single computation used both for matching and as the new value when advancing the world's own record
        public static string ComputeCurrentGenerationMasterFingerprint(
            this GeneratedTerrainTransferPayload generatedPayload, string serverDataDirectory)
        {
            return GenerationMasterFingerprint.Compute(
                MasterHolder.GenerationMaster.SourceJsonText, MasterHolder.GenerationMaster.SelectedGeneration, serverDataDirectory);
        }

        // 別ビルドのサーバーが配るメタは転送ファイル構成そのものが違い、この先の読み出しが全部ずれる
        // A meta served by another build describes a different transfer layout, which skews every read below it
        // world.json側(サーバー自身のワールド)の版照合は、作り直しを促す文言でTerrainTransferMetaReaderが別に持つ
        // The world.json-side check (the server's own world) lives separately in TerrainTransferMetaReader, whose message demands a regeneration
        public static void ThrowIfGeneratorVersionDiffers(
            this GeneratedTerrainTransferPayload generatedPayload, string worldId)
        {
            WorldGeneratorVersion.ThrowIfDiffers(generatedPayload.GeneratorVersion, worldId);
        }
    }
}
