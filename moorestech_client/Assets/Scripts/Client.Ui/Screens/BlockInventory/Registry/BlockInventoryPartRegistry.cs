using System;
using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using UnityEngine;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 差し込まれた工場から本文・題の操作・列車の本文を引く。未登録の Kind は標準の本文へ逃げ、警告を 1 回だけ出す
    /// Resolves bodies, title actions and the train body from plugged factories; unregistered kinds fall back to the standard body with a one-time warning
    /// </summary>
    public sealed class BlockInventoryPartRegistry
    {
        private readonly Dictionary<BlockInventoryBodyKind, IBlockInventoryBodyFactory> _bodies = new();
        private readonly Dictionary<BlockInventoryTitleActionKind, IBlockInventoryTitleActionFactory> _titleActions = new();
        private readonly ITrainInventoryBodyFactory _trainBody;   // 登録 0 本は null（S4-train 未着の間。CreateTrainBody の中だけで分ける）
        private readonly SectionStackBodyFactory _sectionStack;
        private readonly HashSet<string> _warned = new();

        public BlockInventoryPartRegistry(IReadOnlyList<IBlockInventoryBodyFactory> bodies, IReadOnlyList<IBlockInventoryTitleActionFactory> titleActions, IReadOnlyList<ITrainInventoryBodyFactory> trainBodies, SectionStackBodyFactory sectionStack)
        {
            // 二重登録と既定の本文の上書きは起動時に止める（どの登録が衝突したかを例外の文で示す）
            // Stop at startup on duplicates or an override of the default body (the message names the colliding registration)
            foreach (var body in bodies)
            {
                if (body.Kind == BlockInventoryBodyKind.SectionStack) throw new InvalidOperationException($"SectionStack の本文は S4-alpha の既定。差し込めない: {body.GetType().Name}");
                if (!_bodies.TryAdd(body.Kind, body)) throw new InvalidOperationException($"本文の Kind が二重登録: {body.Kind} {body.GetType().Name}");
            }
            foreach (var action in titleActions)
            {
                if (!_titleActions.TryAdd(action.Kind, action)) throw new InvalidOperationException($"題の操作の Kind が二重登録: {action.Kind} {action.GetType().Name}");
            }
            if (1 < trainBodies.Count) throw new InvalidOperationException($"列車の本文は 1 本だけ登録する: {trainBodies.Count} 本");
            _trainBody = trainBodies.Count == 1 ? trainBodies[0] : null;
            _sectionStack = sectionStack;
        }

        public IBlockInventoryPart CreateBody(OpenBlockInventoryPanel panel, BlockInventoryBodyMount mount)
        {
            var kind = panel.Components.Body;
            if (kind == BlockInventoryBodyKind.SectionStack) return _sectionStack.Create(panel.BlockType, mount);
            if (_bodies.TryGetValue(kind, out var factory)) return factory.Create(mount);
            WarnOnce($"body:{kind}", $"[BlockInventory] 本文 {kind} の登録が無いので標準の本文で描く（blockType={panel.BlockType}）");
            return _sectionStack.Create(panel.BlockType, mount);
        }

        // 題の操作を持たない（None）か登録が無ければ空の部分。登録漏れだけ警告する（「無い」は None 1 本で表し null を使わない）
        // An empty part when the kind is None or unregistered; only a missing registration warns ("none" is the None kind alone, never null)
        public IBlockInventoryPart CreateTitleAction(BlockInventoryTitleActionKind kind)
        {
            // silent-ok: 題の操作を持たない blockType が大半（Web も描かない） / 登録漏れは下の警告で出る
            if (kind == BlockInventoryTitleActionKind.None) return new EmptyBlockInventoryPart();
            if (_titleActions.TryGetValue(kind, out var factory)) return factory.Create();
            WarnOnce($"title:{kind}", $"[BlockInventory] 題の操作 {kind} の登録が無いので出さない");
            return new EmptyBlockInventoryPart();
        }

        public ITrainInventoryPart CreateTrainBody()
        {
            if (_trainBody != null) return _trainBody.Create();
            WarnOnce("train", "[BlockInventory] 列車の本文の登録が無いので題とエラー行だけを出す");
            return new EmptyTrainInventoryPart();
        }

        private void WarnOnce(string key, string message)
        {
            if (_warned.Add(key)) Debug.LogWarning(message);
        }
    }
}
