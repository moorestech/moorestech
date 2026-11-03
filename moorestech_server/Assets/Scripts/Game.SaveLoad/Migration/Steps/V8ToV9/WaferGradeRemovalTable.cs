using System;
using System.Collections.Generic;

namespace Game.SaveLoad.Migration.Steps.V8ToV9
{
    /// <summary>
    /// ウェハグレード廃止で消えるG2〜G5ウェハと同じ工程のG1、継承をやめたv8のEUV露光・ダイシングのレシピ
    /// The G2-G5 wafers removed with wafer grades mapped to their process's G1, and the v8 EUV exposure and dicing recipes that stopped inheriting levels
    /// </summary>
    /// <remarks>
    /// v8 マスタの値。マスタ側で GUID を変えないこと。ステップはマスタを引かないので直に持つ（moorestech_master server_v8 items.json の levelFamilies・machineRecipes.json、5c5ccfe）
    /// Values from the v8 master; never change these GUIDs on the master side. Steps never read the master, so they are held directly (moorestech_master server_v8 items.json levelFamilies and machineRecipes.json at 5c5ccfe)
    /// </remarks>
    public static class WaferGradeRemovalTable
    {
        private static readonly Guid TransistorWaferG1 = Guid.Parse("019e3c14-b270-716f-84e5-c0b06a97592b");
        private static readonly Guid OneLayerWaferG1 = Guid.Parse("019e3c25-78f1-70ec-be1b-2c2cdd9297bb");
        private static readonly Guid TwoLayerWaferG1 = Guid.Parse("01a17356-2aff-701f-9a1c-1745ca409137");
        private static readonly Guid CompletedWaferG1 = Guid.Parse("019e3b02-b9fd-73c7-9538-ef13b0f755f1");

        public static readonly IReadOnlyDictionary<Guid, Guid> RemovedToBaseWaferGuids = new Dictionary<Guid, Guid>
        {
            // トランジスタ設置済みウェハG2〜G5
            // Transistor-placed wafer G2-G5
            [Guid.Parse("01a17356-2aff-701f-9a1c-173dd8d81157")] = TransistorWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-173e586a97c8")] = TransistorWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-173f4e56a08c")] = TransistorWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-17400907fcdb")] = TransistorWaferG1,
            // 1層パターンウェハG2〜G5
            // One-layer pattern wafer G2-G5
            [Guid.Parse("01a17356-2aff-701f-9a1c-1741f04fc54a")] = OneLayerWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-1742daa8f03b")] = OneLayerWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-1743a3e0faa8")] = OneLayerWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-17440811ab88")] = OneLayerWaferG1,
            // 2層パターンウェハG2〜G5
            // Two-layer pattern wafer G2-G5
            [Guid.Parse("01a17356-2aff-701f-9a1c-17463137c0e6")] = TwoLayerWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-1747727e8dd7")] = TwoLayerWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-17489944bcb8")] = TwoLayerWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-1749778cc017")] = TwoLayerWaferG1,
            // 完成パターンウェハG2〜G5
            // Completed pattern wafer G2-G5
            [Guid.Parse("01a17356-2aff-701f-9a1c-174aa306c5ed")] = CompletedWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-174b4e200c10")] = CompletedWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-174cedcce469")] = CompletedWaferG1,
            [Guid.Parse("01a17356-2aff-701f-9a1c-174dce4a2f06")] = CompletedWaferG1,
        };

        // v8 で継承レシピだったEUV露光3工程とダイシング。版9では継承しないので保存済みの継承レベルは捨てる
        // The three EUV exposure steps and dicing that inherited levels in v8; version 9 does not inherit, so saved levels are dropped
        public static readonly IReadOnlyCollection<Guid> DroppedInheritanceRecipeGuids = new HashSet<Guid>
        {
            Guid.Parse("01a17363-05b5-72d8-8a5d-a0c245c7ebb6"),
            Guid.Parse("01a17363-05b5-72d8-8a5d-a0c31f51aa60"),
            Guid.Parse("01a17363-05b5-72d8-8a5d-a0c41f603b85"),
            Guid.Parse("01a17363-05b5-72d8-8a5d-a0c5f18cd451"),
        };
    }
}
