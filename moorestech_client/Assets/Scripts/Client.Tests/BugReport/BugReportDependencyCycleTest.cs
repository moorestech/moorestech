using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.BugReport
{
    // バグ報告の確保系がUI状態機械へ環を作っていないことの回帰ガード
    // 環があるとVContainerの生成が Circular dependency で落ち、ゲームが起動しなくなる（実測2026-09-12）
    // Regression guard that the bug-report capture graph creates no cycle back into the UI state machine
    // A cycle makes VContainer fail with Circular dependency and the game never boots (observed 2026-09-12)
    public class BugReportDependencyCycleTest
    {
        // DIの登録で解決先が決まる分だけ、コンストラクタの型からは辿れないので対応表を持つ
        // Only the registrations know these targets, so the map supplies what constructor types cannot
        private static readonly Dictionary<Type, Type> ImplementationByInterface = new()
        {
            { typeof(IBugReportCaptureSources), typeof(BugReportCaptureSources) },
        };

        [Test]
        public void ポーズメニューから辿るコンストラクタ依存に環が無い()
        {
            var path = new List<Type>();
            var cycle = FindCycle(typeof(PauseMenuStateService), new HashSet<Type>(), path);
            Assert.IsNull(cycle, $"依存が循環している: {cycle}");
        }

        [Test]
        public void 確保元はUIStateControlをコンストラクタで受け取らない()
        {
            var parameters = typeof(BugReportCaptureSources).GetConstructors().Single().GetParameters();
            Assert.IsFalse(parameters.Any(parameter => parameter.ParameterType == typeof(Client.Game.InGame.UI.UIState.UIStateControl)),
                "確保元がUIStateControlを受け取ると、UI状態機械→ポーズメニュー→確保元で生成が循環する");
        }

        // クライアントの自前の型だけを辿る。Unity・外部ライブラリの型はDIの環に関与しない
        // Walks only first-party client types; Unity and third-party types take no part in the cycle
        private static string FindCycle(Type type, HashSet<Type> visiting, List<Type> path)
        {
            var resolved = ImplementationByInterface.TryGetValue(type, out var implementation) ? implementation : type;
            path.Add(resolved);
            if (!visiting.Add(resolved)) return string.Join(" -> ", path.Select(entry => entry.Name));

            foreach (var dependency in DependenciesOf(resolved))
            {
                if (!IsFirstPartyClientType(dependency)) continue;
                var cycle = FindCycle(dependency, visiting, path);
                if (cycle != null) return cycle;
            }

            visiting.Remove(resolved);
            path.RemoveAt(path.Count - 1);
            return null;
        }

        // MonoBehaviourは[Inject]メソッドで依存を受けるため、コンストラクタだけを見ると環を見落とす
        // MonoBehaviours take dependencies through [Inject] methods, so constructors alone would miss the cycle
        private static IEnumerable<Type> DependenciesOf(Type type)
        {
            var constructor = type.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length).FirstOrDefault();
            var fromConstructor = constructor == null ? Enumerable.Empty<Type>() : constructor.GetParameters().Select(parameter => parameter.ParameterType);
            var fromInjectMethods = type.GetMethods()
                .Where(method => method.IsDefined(typeof(InjectAttribute), true))
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType));
            return fromConstructor.Concat(fromInjectMethods);
        }

        private static bool IsFirstPartyClientType(Type type)
        {
            return type.Namespace != null && type.Namespace.StartsWith("Client.", StringComparison.Ordinal);
        }
    }
}
