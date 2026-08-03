using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil.Cil;
using Mono.Cecil;

namespace DeadMemberAudit.Cancellation;

// キャンセルの3形（未伝搬・async void・CTS作りっぱなし）をproductionアセンブリから拾う
// Picks the three cancellation shapes out of production assemblies: dropped tokens, async void, and dangling CTS
public sealed class CancellationScanner
{
    private static readonly OpCode[] CallOpCodes = { OpCodes.Call, OpCodes.Callvirt, OpCodes.Newobj };

    private readonly AssemblyClassifier _classifier;
    private readonly SourceLocator _sourceLocator;
    private readonly TypeSourceLocator _typeSourceLocator;
    private readonly Dictionary<string, MethodDefinition> _asyncMethodByStateMachine = new(StringComparer.Ordinal);

    public CancellationScanner(AssemblyClassifier classifier, SourceLocator sourceLocator, TypeSourceLocator typeSourceLocator)
    {
        _classifier = classifier;
        _sourceLocator = sourceLocator;
        _typeSourceLocator = typeSourceLocator;
    }

    public List<CancellationFinding> Scan(IReadOnlyList<LoadedAssembly> assemblies)
    {
        var production = assemblies.Where(assembly => assembly.Category.IsProduction()).ToList();
        IndexStateMachines(production);

        var findings = new List<CancellationFinding>();
        foreach (var assembly in production)
        {
            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                if (TypeTraits.IsGeneratedCode(type)) continue;
                foreach (var method in type.Methods)
                {
                    CollectAsyncVoid(method, assembly.Name, findings);
                    CollectDroppedTokens(method, assembly.Name, findings);
                }
            }
        }

        findings.AddRange(ScanTokenSources(assemblies));
        return findings;
    }

    // asyncの本体は生成された状態機械へ移るので、走査時に元のメソッドへ辿り直せるようにする
    // An async body moves into a generated state machine, so keep a way back to the original method while scanning
    private void IndexStateMachines(IReadOnlyList<LoadedAssembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                foreach (var method in type.Methods)
                {
                    var stateMachine = TypeTraits.StateMachineType(method);
                    if (stateMachine != null) _asyncMethodByStateMachine[stateMachine.FullName] = method;
                }
            }
        }
    }

    // Unityイベント関数はシグネチャ上voidしか選べないため除外する。それ以外の正当例外は裁定に回す
    // Unity event functions can only be void by signature, so they are excluded; other legitimate cases go to adjudication
    private void CollectAsyncVoid(MethodDefinition method, string assemblyName, List<CancellationFinding> findings)
    {
        if (TypeTraits.StateMachineType(method) == null || method.ReturnType.FullName != AuditConstants.VoidFullName) return;
        if (TypeTraits.IsUnityMessageName(method.Name) && TypeTraits.IsUnityObjectType(method.DeclaringType)) return;

        findings.Add(new CancellationFinding(assemblyName, method.DeclaringType.FullName,
            $"{method.DeclaringType.Name}.{method.Name}", MethodLocation(method),
            CancellationIssue.AsyncVoid, "async void（UniTaskVoid + .Forget() へ）"));
    }

    // asyncのスタブ本体は隠しシーケンスポイントしか持たないことがあるので、状態機械側と型宣言へ順に落とす
    // An async stub can carry only hidden sequence points, so the state machine and then the type declaration are used as fallbacks
    private string MethodLocation(MethodDefinition method)
    {
        var direct = _sourceLocator.DisplayLocation(method);
        if (direct.Length > 0) return direct;

        var stateMachine = TypeTraits.StateMachineType(method);
        var moveNext = stateMachine?.Methods.FirstOrDefault(candidate => candidate.Name == "MoveNext");
        var fromStateMachine = moveNext == null ? string.Empty : _sourceLocator.DisplayLocation(moveNext);
        return fromStateMachine.Length > 0 ? fromStateMachine : _typeSourceLocator.DisplayLocation(method.DeclaringType);
    }

    private void CollectDroppedTokens(MethodDefinition method, string assemblyName, List<CancellationFinding> findings)
    {
        if (!method.HasBody) return;
        var owner = _asyncMethodByStateMachine.TryGetValue(method.DeclaringType.FullName, out var asyncMethod) ? asyncMethod : method;

        // トークンを持っていない呼び出し元は伝搬しようがない。ここで母集団を絞る
        // A caller with no token has nothing to propagate, so the population is narrowed here
        var holder = TokenHolderReason(owner, method);
        if (holder.Length == 0) return;

        foreach (var instruction in method.Body.Instructions)
        {
            if (!CallOpCodes.Contains(instruction.OpCode) || instruction.Operand is not MethodReference reference) continue;
            if (reference.FullName == owner.FullName) continue;

            var form = DroppedTokenForm(reference, instruction);
            if (form.Length == 0) continue;
            findings.Add(new CancellationFinding(assemblyName, owner.DeclaringType.FullName,
                $"{owner.DeclaringType.Name}.{owner.Name} → {reference.DeclaringType.Name}.{reference.Name}",
                _sourceLocator.LocationOf(method, instruction), CancellationIssue.TokenNotPassed, $"{form} / 呼び出し元: {holder}"));
        }
    }

    // MonoBehaviourは常にGetCancellationTokenOnDestroyでトークンを作れるので、持ち主として数える
    // A MonoBehaviour can always mint a token through GetCancellationTokenOnDestroy, so it counts as a holder
    private static string TokenHolderReason(MethodDefinition owner, MethodDefinition body)
    {
        if (TokenPropagationRules.HasTokenParameter(owner)) return "CT引数あり";
        if (TokenPropagationRules.AccessesTokenSourceField(body) || TokenPropagationRules.AccessesTokenSourceField(owner)) return "CTSフィールド参照あり";
        if (!owner.IsStatic && TypeTraits.IsUnityObjectType(TypeTraits.OutermostType(owner.DeclaringType))) return "Unityオブジェクト（GetCancellationTokenOnDestroy可）";
        return string.Empty;
    }

    // オーバーロード探索は宣言型の解決が要るので、moorestechアセンブリに限る
    // Finding an overload requires resolving the declaring type, so it is limited to moorestech assemblies
    private string DroppedTokenForm(MethodReference reference, Instruction instruction)
    {
        if (!TokenPropagationRules.IsAwaitable(reference.ReturnType)) return string.Empty;
        if (TokenPropagationRules.HasTokenParameter(reference)) return TokenPropagationRules.EmptyTokenForm(instruction);
        if (!_classifier.Classify(MemberKey.ScopeNameOf(reference.DeclaringType)).IsMoorestech()) return string.Empty;

        var callee = AssemblyLoader.TryResolve(reference);
        if (callee == null) return string.Empty;
        return TokenPropagationRules.FindTokenOverload(callee) != null ? "CT引数つきオーバーロードがあるのにCT無し版を呼んでいる" : string.Empty;
    }

    private List<CancellationFinding> ScanTokenSources(IReadOnlyList<LoadedAssembly> assemblies)
    {
        var tracker = new CtsFieldTracker();
        tracker.CollectFields(assemblies);
        tracker.CollectReleases(assemblies);

        return tracker.UnreleasedFields().Select(field => new CancellationFinding(
            field.Module.Assembly.Name.Name, field.DeclaringType.FullName, $"{field.DeclaringType.Name}.{field.Name}",
            _typeSourceLocator.DisplayLocation(field.DeclaringType),
            CancellationIssue.CancellationTokenSourceNotReleased, "Cancel/Disposeがどのアセンブリにも無い")).ToList();
    }
}
