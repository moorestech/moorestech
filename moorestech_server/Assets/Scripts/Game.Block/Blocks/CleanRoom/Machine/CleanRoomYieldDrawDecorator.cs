using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.OutputRewrite;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // チップ工程の抽選を実現出力の確定直後に適用する。役割はcleanRoom.jsonの役割表で引き、役割の無いレシピは素通しする
    // Applies chip-process draws right after outputs are realized; roles come from cleanRoom.json, and recipes without a role pass through
    internal class CleanRoomYieldDrawDecorator : IRealizedOutputDecorator, ICleanRoomEffectLookup
    {
        // 抽選シードのブロック固有カウンタ。セーブへ持ち出すため公開する
        // Per-block counter feeding the draw seed; exposed because it is serialized
        public uint CycleCount { get; private set; }

        // 最後に押し込まれた部屋の効果。抽選・processor の停止判定・抽選の中身の表示（B6）がこれ1つを読む
        // The last pushed room effect; the draw, the processor's halt check and the draw-detail display (B6) all read this one value
        public CleanRoomEffect CurrentEffect { get; private set; }

        private readonly BlockInstanceId _blockInstanceId;

        public CleanRoomYieldDrawDecorator(BlockInstanceId blockInstanceId, uint cycleCount)
        {
            _blockInstanceId = blockInstanceId;
            CycleCount = cycleCount;

            // 部屋の効果が届く前の値は CleanRoomEffect が1か所で決める（A7 の停止理由の状態も同じ口を読む）
            // CleanRoomEffect alone decides the value before any room effect arrives (A7's halt-reason state reads the same factory)
            CurrentEffect = CleanRoomEffect.BeforeFirstPush();
        }

        public void SetCleanRoomEffect(CleanRoomEffect effect)
        {
            CurrentEffect = effect;
        }

        public List<IItemStack> Decorate(MachineRecipeMasterElement recipe, int? inheritedLevel, MachineModuleEffect moduleEffect, int setCount, List<IItemStack> realizedOutputs, out IRealizedOutputDrawRecord drawRecord)
        {
            // 役割の無いレシピは抽選せずカウンタも進めない
            // A recipe with no role draws nothing and leaves the counter alone
            drawRecord = null;
            if (!MasterHolder.CleanRoomMaster.TryGetRecipeRole(recipe.MachineRecipeGuid, out var role)) return realizedOutputs;

            // 抽選に使う率を表示と同じ式で記録し、出力待ちの間はこの値を確定済みの効果として見せる
            // Record the draw's rates through the display's own formula, shown as the fixed effect while the outputs wait
            var context = StartCycle();
            drawRecord = CleanRoomYieldPreview.Evaluate(role, context.ClassYield, moduleEffect, context, inheritedLevel, out var outputs);
            return outputs;

            #region Internal

            CleanRoomYieldRollContext StartCycle()
            {
                // processor は稼働できない間は加工を始めないので、抽選のときは必ずクラス値がある。無ければ品質の効果が黙って消えるので止める
                // The processor never starts work while it cannot operate, so class values always exist here; without them the quality effect would vanish silently, so stop
                if (!CurrentEffect.TryGetClassYield(out var classYield))
                {
                    throw new InvalidOperationException($"稼働できない清浄室機械で抽選が起きました。 block={_blockInstanceId} recipe={recipe.MachineRecipeGuid} operability={CurrentEffect.Operability}");
                }

                // 抽選のたびにカウンタを進め、ブロック固有で再現可能なシードにする
                // Advance the counter on every draw to keep the seed per-block and reproducible
                CycleCount++;
                var random = new CleanRoomDrawRandom(((long)_blockInstanceId.AsPrimitive() << 20) ^ CycleCount);

                // 書き換え器は工場の1セット目の写しを書き換え、工場が決めたセット数だけ並べる（生産性の追加セットも同じ抽選結果）
                // Rewriters edit a copy of the factory's first set and repeat it by the factory's set count (the extra set shares the draw)
                var factorySet = realizedOutputs.GetRange(0, recipe.OutputItems.Length);
                return new CleanRoomYieldRollContext(classYield, moduleEffect, factorySet, setCount, random);
            }

            #endregion
        }
    }
}
