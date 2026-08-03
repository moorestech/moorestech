namespace DeadMemberAudit.Model;

// 判定に使う名前定数の集約先。分類・除外規則はすべてここを参照する
// Single home for the name constants used by classification and exclusion rules
public static class AuditConstants
{
    public const string DefaultAssembliesRelativePath = "moorestech_client/Library/ScriptAssemblies";

    // asmdefを探す既定のソースルート。ここに無いアセンブリは外部扱い。配置サイドもこの所在から導く
    // Default source roots scanned for asmdefs; assemblies outside them are external, and the side comes from this location
    public static readonly (string Path, AssemblySide Side)[] SourceRoots =
    {
        ("moorestech_server/Assets/Scripts", AssemblySide.Server),
        ("moorestech_client/Assets/Scripts", AssemblySide.Client),
    };

    // 解析対象はクライアントプロジェクトのScriptAssembliesなので、既定アセンブリはclient側
    // The analysed ScriptAssemblies belong to the client project, so the default assemblies are client-side
    public static readonly string[] DefaultAssemblyNames = { "Assembly-CSharp", "Assembly-CSharp-Editor" };

    // アセンブリ名の部分一致で分類する。判定順は Default→Test→Debug→Editor
    // Assembly names are classified by substring, evaluated Default -> Test -> Debug -> Editor
    public static readonly string[] TestNameFragments = { "Tests", "Test", "Playtest" };
    public static readonly string[] DebugNameFragments = { "Debug" };
    public static readonly string[] EditorNameFragments = { "Editor" };

    // 外部Modや動的コードから呼ばれる公開APIアセンブリ。IL上の呼び出し元は原理的に存在しない
    // Public-API assemblies called from external mods or dynamic code, so IL call sites never exist
    public static readonly string[] ExternalApiAssemblies = { "Mod.Base", "Mod.Config" };

    // SourceGenerator生成コードとコンパイラ注入型の名前空間。実体のソースがリポジトリに無い
    // Namespaces of source-generated and compiler-injected code that has no hand-written source in the repository
    public static readonly string[] GeneratedNamespacePrefixes =
    {
        "Mooresmaster.", "CommandForgeGenerator.", "UnitGenerator.",
        "System.Runtime.CompilerServices.", "Microsoft.CodeAnalysis.",
    };

    // 名前空間なしで各アセンブリへ複製される生成物
    // Generator artifacts duplicated into every assembly without a namespace
    public static readonly string[] GeneratedTypeNames = { "GenerateError" };

    public const string AttributeFullName = "System.Attribute";
    public const string MonoBehaviourFullName = "UnityEngine.MonoBehaviour";
    public const string ScriptableObjectFullName = "UnityEngine.ScriptableObject";

    // Unityが生成・管理する型の基底。コンストラクタはAddComponent/デシリアライズ経由で呼ばれる
    // Base types Unity creates and owns; their constructors run via AddComponent or deserialization
    public static readonly string[] UnityObjectBaseTypes =
    {
        "UnityEngine.Object", "UnityEngine.Component", "UnityEngine.Behaviour",
        MonoBehaviourFullName, ScriptableObjectFullName,
    };

    // MonoBehaviour/ScriptableObject派生でエンジンが直接呼ぶメッセージ関数
    // Engine-invoked message functions on MonoBehaviour / ScriptableObject subclasses
    public static readonly string[] UnityMessageMethods =
    {
        "Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy",
        "OnApplicationQuit", "OnApplicationPause", "OnApplicationFocus", "OnGUI", "OnValidate", "Reset",
        "OnBecameVisible", "OnBecameInvisible", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderObject",
        "OnRenderImage", "OnWillRenderObject", "OnAnimatorMove", "OnAnimatorIK", "OnParticleCollision",
        "OnParticleTrigger", "OnTransformChildrenChanged", "OnTransformParentChanged", "OnBeforeTransformParentChanged",
        "OnLevelWasLoaded", "OnAudioFilterRead", "OnJointBreak", "OnJointBreak2D", "OnDisconnectedFromServer",
    };

    // 前方一致で判定するUnityメッセージ関数（Enter/Stay/Exitの3種を持つもの）
    // Unity message functions matched by prefix because they come in Enter/Stay/Exit variants
    public static readonly string[] UnityMessagePrefixes =
    {
        "OnTrigger", "OnCollision", "OnMouse", "OnDrawGizmos", "OnControllerColliderHit", "OnPointer", "OnDrag",
    };

    // 属性が付いていればフレームワーク側から呼ばれる。名前空間は問わず単純名で照合する
    // Attribute presence means the framework calls it; matched by simple name regardless of namespace
    public static readonly string[] FrameworkInvokedAttributes =
    {
        "TestAttribute", "TestCaseAttribute", "TestCaseSourceAttribute", "UnityTestAttribute", "TheoryAttribute",
        "SetUpAttribute", "TearDownAttribute", "OneTimeSetUpAttribute", "OneTimeTearDownAttribute",
        "UnitySetUpAttribute", "UnityTearDownAttribute", "ValuesAttribute",
        "ContextMenuAttribute", "MenuItemAttribute", "InitializeOnLoadMethodAttribute",
        "RuntimeInitializeOnLoadMethodAttribute", "DidReloadScripts", "OnOpenAssetAttribute",
        "InjectAttribute", "SerializationConstructorAttribute", "JsonConstructorAttribute",
        "PostProcessBuildAttribute", "PostProcessSceneAttribute", "PreserveAttribute",
    };

    // これらの属性を持つ型は、シリアライザがメンバーを反射的に読み書きする
    // Types carrying these attributes have their members read and written reflectively by a serializer
    public static readonly string[] SerializedTypeAttributes =
    {
        "MessagePackObjectAttribute", "JsonObjectAttribute", "DataContractAttribute",
    };

    // これらの属性を持つメンバーは、シリアライザ経由でのみ触られる
    // Members carrying these attributes are only touched through a serializer
    public static readonly string[] SerializedMemberAttributes =
    {
        "KeyAttribute", "IgnoreMemberAttribute", "JsonPropertyAttribute", "JsonIgnoreAttribute",
        "DataMemberAttribute", "OptionAttribute", "SerializeField", "SerializeReference",
    };

    // DIコンテナへの登録メソッド。ジェネリック引数の型はコンテナが反射的にnewする
    // Container registration methods whose generic arguments are reflectively constructed by the container
    public static readonly string[] DiRegistrationMethods =
    {
        "AddSingleton", "AddTransient", "AddScoped", "AddSingletonForwarding",
        "Register", "RegisterEntryPoint", "RegisterInstance", "RegisterComponent",
        "RegisterComponentInHierarchy", "RegisterComponentInNewPrefab", "RegisterBindingComposite",
    };

    // ジェネリック引数の型が反射的に生成・充填されるシリアライズAPI
    // Serialization APIs whose generic argument types are reflectively constructed and populated
    public static readonly string[] ReflectiveSerializationMethods =
    {
        "Deserialize", "DeserializeObject", "Serialize", "SerializeObject", "FromJson", "ToJson",
        "FromJsonOverwrite", "ConvertTo", "GetUninitializedObject",
    };

    public const string CancellationTokenFullName = "System.Threading.CancellationToken";
    public const string CancellationTokenSourceFullName = "System.Threading.CancellationTokenSource";
    public const string VoidFullName = "System.Void";

    // 待てる戻り値の型。トークン伝搬が寿命の話になるのはこの形の呼び先だけ
    // Awaitable return types; token propagation is a lifetime question only for callees shaped like these
    public static readonly string[] AwaitableReturnPrefixes =
    {
        "System.Threading.Tasks.Task", "System.Threading.Tasks.ValueTask",
        "Cysharp.Threading.Tasks.UniTask", "System.Collections.Generic.IAsyncEnumerable",
    };

    // CTSの後始末とみなす呼び出し。どれか1本でも通れば作りっぱなしではない
    // Calls that count as releasing a CTS; any one of them means it is not left dangling
    public static readonly string[] CancellationTokenSourceReleaseMethods = { "Cancel", "CancelAsync", "Dispose" };

    // 非同期メソッドの実体がどの生成型に移されたかを示す属性
    // Attributes pointing at the generated type that holds an async method's real body
    public static readonly string[] StateMachineAttributes = { "AsyncStateMachineAttribute", "IteratorStateMachineAttribute" };
}
