# DeadMemberAudit

コンパイル済みDLLのIL解析で「死にpublicメンバー」「公開範囲過剰」「サーバー配置ミス」「キャンセル未伝搬」を
検出する監査ツール。Mono.Cecilでシグネチャを厳密に照合するため、`Initialize`のようなありふれた名前でも
参照過剰カウントで見逃すことがない（名前ベースgrepに対する優位点）。

AGENTS.md規範「デバッグ/テスト専用publicをプロダクションに残さない」「受益者なき抽象の禁止」と、
`reviewers/core-cs-async-cancellation.md` の機械検出が目的。

## 使い方

```bash
cd tools/DeadMemberAudit
dotnet run                       # 既定: moorestech_client/Library/ScriptAssemblies
dotnet run -- /path/to/ScriptAssemblies
```

markdownレポートを標準出力と `tools/DeadMemberAudit/report.md` の両方へ書く（`report.md`はgit管理外）。
実行前にUnityでコンパイルを済ませておくこと。解析対象は**コンパイル済みDLLであってソースではない**ため、
未コンパイルの変更は結果に反映されない。

## 出力

| リスト | 検出内容 | 対応rule（ゲート） |
| --- | --- | --- |
| 1「参照0」 | どのアセンブリからも呼ばれていないpublicメンバー | `dead-member-unused` |
| 2「非production参照のみ」 | テスト/デバッグ/エディタ/デフォルトからしか呼ばれていないpublicメンバー | `dead-member-nonproduction` |
| 3-A「private候補」 | 参照は実在するが、全参照が宣言型の中だけ | `dead-member-overpublic-private` |
| 3-B「internal候補」 | 参照は実在するが、全参照が宣言アセンブリの中だけ | `dead-member-overpublic-internal` |
| 4-A「サーバー配置ミス」 | server側で宣言されたのにclient側からしか使われていない型 | `placement-mismatch` |
| 4-B「登録のみ・解決者なし」 | server側の接点がDI登録だけの型 | `placement-registration-only` |
| 5-A「CT未伝搬」 | トークンを持つ呼び出し元がCTを渡していない呼び出しサイト | `ct-not-passed` |
| 5-B「async void」 | `[AsyncStateMachine]`付きでvoid返しのメソッド | `ct-async-void` |
| 5-C「CTS作りっぱなし」 | CTSフィールドにCancel/Disposeがどこにも無い | `cts-not-released` |
| 6-A「単一呼び出し元ヘルパ」 | 同一型の1メソッドからしか呼ばれていないprivateメソッド | `single-caller-helper` |
| 6-B「参照0private」 | どこからも呼ばれていないprivateメソッド | `dead-private-member` |

全リストとも **2列目が対象・最終列が裁定用の文脈** で、`宣言場所` に `` `path:line` `` を持つ
（`.claude/skills/moores-code-review/scripts/dead_member_gate.py` がこの2つの位置に依存している）。
リスト3-Bは件数が多いので、宣言型ごとの集約表を先に出し、全件は`<details>`に畳んで出す（ゲートは畳んだ表も読む）。

## アセンブリ分類

`moorestech_server/Assets/Scripts` と `moorestech_client/Assets/Scripts` 配下の`.asmdef`の`name`フィールドを実読みして
「moorestechアセンブリ」の集合を作る（ファイル名とアセンブリ名が食い違う例があるため。例: `Tests.asmdef` → `Client.Tests`）。
そこに無いDLLは外部扱いで、参照解決にしか使わない。

| 分類 | 判定 | 参照元としての扱い |
| --- | --- | --- |
| Default | `Assembly-CSharp` / `Assembly-CSharp-Editor` | 非production |
| Test | 名前に`Tests`/`Test`/`Playtest`を含む | 非production |
| Debug | 名前に`Debug`を含む | 非production |
| Editor | 名前に`Editor`を含む | 非production |
| Production | 上記以外のmoorestechアセンブリ | **production（生存の根拠になる）** |

判定順はDefault→Test→Debug→Editor。定数は`Model/AuditConstants.cs`に集約。

### server / client の配置サイド

役割分類とは別に、**asmdefが実際に置かれているディレクトリ**から配置サイドを決める（名前パターンは使わない。
`ClassLibrary`のようにServer/Clientを名前に含まないアセンブリがあるため）。

| asmdefの所在 | サイド |
| --- | --- |
| `moorestech_server/Assets/Scripts` 配下 | server |
| `moorestech_client/Assets/Scripts` 配下 | client |
| `Assembly-CSharp` / `Assembly-CSharp-Editor` | client（解析対象がクライアントプロジェクトのため） |

分類表はレポートのサマリに全件出る。リスト4の判定はこの表だけを見る。

サーバーのスクリプト群はUPMパッケージ`tech.moores.server`としてクライアントプロジェクトにも取り込まれるため、
`moorestech_client/Library/ScriptAssemblies`ひとつでサーバー・クライアント両方の参照が揃う。
別途サーバー単体プロジェクトを解析する必要はない。

## 母集団と参照の数え方

- **母集団**: productionアセンブリで宣言されたpublicメソッド・publicコンストラクタ・publicプロパティ。
  internalは対象外（第1版）。internal型の中のpublicメンバーは母集団に含む
- **参照**: 全moorestechアセンブリ（テスト・デバッグ・エディタ・デフォルト含む）の全メソッド本体のILを走査し、
  `Call`/`Callvirt`/`Newobj`/`Ldftn`/`Ldvirtftn`/`Ldtoken`のMethodReferenceをResolveして宣言定義に紐付ける
- プロパティはgetter/setterへの参照を合算する。**片側だけ未参照なら死にメンバーではない**（両方0のときだけ候補）
- 自己参照（自分自身を呼ぶだけの再帰）は生存の根拠にしない

## リスト3（公開範囲過剰）の数え方

参照を記録するとき、同時に**参照が宣言型の外へ出たか・宣言アセンブリの外へ出たか**の2フラグを立てる。
ネスト型とラムダのクロージャは**最も外側の型**へ畳んでから比較する（C#は入れ子スコープ間でprivateが見えるため、
`Foo/Bar`から`Foo`のprivateを呼ぶのは合法で、これを「型の外からの参照」と数えると縮小提案が出せなくなる）。

母集団はリスト1/2と同じ（機械除外は全部適用済み）で、そのうち**production参照がある生存メンバー**だけが対象。
以下は縮小するとコンパイルが通らないので候補にしない（除外理由には数えない）:

- interfaceのメンバー（常にpublic）
- `virtual` / `abstract` メンバー（派生側が上書きする）
- 静的コンストラクタ・operator（アクセシビリティを選べない）
- internal型のpublicメンバー → internal候補にはしない（実効的に既にinternal）。private候補にはなる

## リスト4（サーバー配置ミス）の数え方

母集団は**server側production アセンブリのトップレベル型**（ネスト型は親と一緒に動くので単独では扱わない）。
型の使われ方を性質ごとに3種類数える:

| 種別 | 数える対象 |
| --- | --- |
| 解決者（resolver） | フィールド型・プロパティ型・引数型・戻り値型・ローカル変数型・メンバー呼び出し・`typeof`・DI登録**以外**のジェネリック実引数（`GetService<T>()`等）・フィールドの持ち主（`X.EventTag`の`X`） |
| DI登録 | `AddSingleton`/`Register`系のジェネリック実引数 |
| 実装 | 基底型・実装interface（供給側なので解決者に数えない） |

判定は **server側の解決者が0** であることが起点。そのうえで、

- server側にDI登録がある → **4-B「登録のみ・解決者なし」**（PR1095 `IBlueprintCatalogSource` と同型）
- そうでなくclient側の解決者がある → **4-A「配置ミス」**

`AddSingleton<IFoo, Foo>()` のように1回の登録に複数の型が並ぶ場合、実装型は自分の名前ではなく
サービス型の名前で解決されるため、**同じ登録に並んだ型どうしを結んで解決者数を合算する**
（これをやらないと`WorldBlockDatastore`等の具象型が全部4-Bに載る）。

## リスト5（キャンセル）の数え方

このリポジトリで実際に使われているCTの受け渡し形をgrepで調べた結果、`AttachExternalCancellation` /
`WithCancellation` は**0件**で、`CancellationToken`引数の明示的な貫通・`CancellationToken.None`・
省略可能引数`CancellationToken ct = default`・`this.GetCancellationTokenOnDestroy()`・`.Forget()` だけが使われている。
対応範囲はこの実態に合わせてある。

- **CT未伝搬**: `async`の本体は生成された状態機械へ移るため、`[AsyncStateMachine]`から状態機械型を引いて
  そちらのILを走査し、指摘は元のメソッドへ帰属させる。呼び出し元が「トークンを持っている」根拠は3つあり、
  どれが効いたかはレポートの`形`列に出る:
  1. `CT引数あり` — メソッドが`CancellationToken`引数を持つ
  2. `CTSフィールド参照あり` — 本体が`CancellationTokenSource`型フィールドを読んでいる
  3. `Unityオブジェクト` — `UnityEngine.Object`派生型の非staticメソッド（`GetCancellationTokenOnDestroy()`で作れる）

  呼び先は**待てる戻り値**（`Task`/`ValueTask`/`UniTask`系/`IAsyncEnumerable`）に限る。
  同期APIのCT引数（MessagePackの`Deserialize`等）は寿命の話ではないため対象外
- **CT無し版の呼び出し**: 呼び先の宣言型に「同名・引数1本多い・末尾が`CancellationToken`」のメソッドがあるのに
  CT無し版を呼んでいる形。宣言型の解決が要るのでmoorestechアセンブリの呼び先に限る
- **`CancellationToken.None`/`default`の受け渡し**: 呼び出し命令の直前が`CancellationToken::get_None()`、
  または`initobj CancellationToken`＋ローカル読み出しの形を見る（省略可能引数の省略も同じ命令列になる）
- **async void**: `[AsyncStateMachine]`付きでvoid返し。Unityイベント関数（`MonoBehaviour`派生の`Start`等）だけ機械除外。
  デリゲート制約による正当な`async void`はverifierの裁定に回す
- **CTS作りっぱなし**: productionの手書き型が持つ`CancellationTokenSource`フィールドのうち、
  そのフィールドを読むメソッドの**どれもが**`Cancel`/`CancelAsync`/`Dispose`を呼んでいないもの。
  `using`は`IDisposable.Dispose`経由で呼ばれるのでそれも後始末として認める

## 機械除外の規則

ILに呼び出し元が現れないメンバーをリストから外す。除外理由別の件数はレポートのサマリに出る。

| 除外理由 | 内容 |
| --- | --- |
| `GeneratedCode` | 生成コード（後述） |
| `ImplicitInterfaceImplementation` | 宣言型と基底型がたどれる全interfaceのメンバーとシグネチャ一致するもの |
| `SerializedMember` | シリアライザが反射的に読み書きするメンバー（後述） |
| `ImplicitDefaultConstructor` | `ldarg.0; call base..ctor; ret`だけの引数なしコンストラクタ（人が書いたソースが無い） |
| `ReflectivelyConstructedType` | `typeof(X)`やシリアライズAPIのジェネリック引数に現れた型のコンストラクタ |
| `DiConstructedType` | DIコンテナ登録型のコンストラクタ（後述） |
| `Override` | `override`メソッド |
| `UnityObjectConstructor` | `UnityEngine.Object`派生型のコンストラクタ（`AddComponent`/デシリアライズで生成される） |
| `ExternalApiAssembly` | `Mod.Base` / `Mod.Config`（外部Mod向け公開API。IL上の呼び出し元が原理的に存在しない） |
| `FrameworkInvokedAttribute` | NUnit系・`MenuItem`・`ContextMenu`・`RuntimeInitializeOnLoadMethod`・`[Inject]`等 |
| `AttributeType` | `System.Attribute`派生型のメンバー（属性はメタデータのblobから参照されILに現れない） |
| `ExplicitInterfaceImplementation` | 明示的interface実装 |
| `UnityMessageFunction` | `MonoBehaviour`/`ScriptableObject`派生型の`Awake`/`Update`/`OnTrigger*`等 |
| `CompilerGenerated` | `IsCompilerControlled`・名前に`<`を含む・`[CompilerGenerated]` |

### 生成コードの判定はPDB基準

名前空間のハードコードではなく、**PDBのシーケンスポイントからソースファイルを引いて**判定する。

1. ファイルが**ディスク上に存在しない** → SourceGeneratorの仮想ファイル
2. 存在してヘッダ10行に`<auto-generated`がある → 生成済みファイル

これで`Mooresmaster.*`（マスタデータ生成）・`CommandForgeGenerator.*`（スキット生成）・
UnitGeneratorが`[UnitOf]`から生やす`BlockId`/`ItemId`等の比較演算子（partialの生成側に置かれる）・
Unity InputSystemが`.inputactions`から吐く`MoorestechInputSettings`が、規則を書き足さずまとめて落ちる。
名前空間・`[GeneratedCode]`によるフォールバック判定も残してある（シンボルが無い環境向け）。

**PDBが読めないとこの判定が効かない。** レポートのサマリに「シンボル無しで読んだアセンブリ」の件数を出しているので、
0でない場合は結果の精度が落ちていると考えること。

## 型フォワーダの循環

Unity/Monoが同梱するファサードDLLには型フォワーダが循環しているものがある（実測では`System.Net.Sockets.Socket`が1件）。
素のCecilはこの循環を`Resolve`→`ExportedType.Resolve`→`Resolve`と無限再帰し、stack overflowでプロセスごと落ちる。
`ForwarderCycleGuardResolver`が解決中の型を追跡して再入時にnullを返すことで打ち切っており、
打ち切った件数はサマリの「循環フォワーダで打ち切った型解決」に出る。
循環した鎖は本当に解決不能なのでnullが正しい答えだが、件数が急に増えた場合は検索ディレクトリ構成を疑うこと。

## DI・リフレクション経路の調査結果

このリポジトリで「ILに呼び出し元が現れないのに実際は呼ばれる」経路を実地調査した結果と、対応する除外規則。

### DIコンテナ（2系統。どちらもコンストラクタを反射的に呼ぶ）

- **サーバー: Microsoft.Extensions.DependencyInjection**
  `Server.Boot/MoorestechServerDIContainerGenerator.cs` に `new ServiceCollection()` と約90本の `AddSingleton`。
  `Game.Context/ServerContext.cs` が静的サービスロケータとして `GetService<T>()` を公開。
  登録型（`WorldBlockDatastore`・`BlockFactory`・各`*TickUpdater`・22本の`*EventPacket`等）は
  **どこにも`new`が無い**。MS.DIが最も引数の多い解決可能なコンストラクタをリフレクションで選ぶ。
- **クライアント: VContainer 1.10.0**
  `Client.Starter/MainGameStarter.cs`（`LifetimeScope`）に約70本の `builder.Register<T>(Lifetime.Singleton)` と
  `RegisterEntryPoint<T>()`。`Client.Game/InGame/Context/DIContainer.cs` が`IObjectResolver`をラップ。
  Zenject・Reflexは不使用。

**対応**: ILの`GenericInstanceMethod`のうちメソッド名が
`AddSingleton`/`AddTransient`/`AddScoped`/`Register`/`RegisterEntryPoint`/`RegisterInstance`/`RegisterComponent*`/
`RegisterBindingComposite` のものを見つけ、そのジェネリック実引数の型を「DI生成型」として記録し、
**その型のpublicコンストラクタを除外**する（`DiConstructedType`）。メソッドは除外しない。

VContainerのライフサイクル（`IInitializable.Initialize`・`IStartable.Start`・`ITickable.Tick`）と
サーバーの`IBootInitializable.Load`/`IPostLoadInitializable.Load`は、interfaceメンバーの実装なので
`ImplicitInterfaceImplementation`規則が自動的に拾う。専用規則は不要。

`[Inject]`によるメソッドインジェクション（`Construct`という命名で14箇所）は`FrameworkInvokedAttribute`で除外。

### シリアライズ

- **MessagePack 3.1.4**: `StandardResolver`（実行時に動的コード生成）。157型の`[MessagePackObject]`、
  459箇所の`[Key(n)]`、124本の`[Obsolete("デシリアライズ用のコンストラクタです…")]`引数なしコンストラクタ。
  `IMessagePackFormatter<T>`実装（`BlockIdMessagePackFormatter`等）も`Serialize`/`Deserialize`が
  フォーマッタ経由でしか呼ばれない
- **Newtonsoft.Json**: 約40箇所の`JsonConvert.DeserializeObject<T>`、191箇所の`[JsonProperty]`。
  `Game.Block/Blocks/**/*SaveJsonObject`群と`Client.WebUiHost`の`*Dto`群が該当
- **Unity JsonUtility / SerializeField**: `[SerializeField]`は431箇所。`.prefab`/`.unity`のYAMLから復元される

**対応**: 型が`[MessagePackObject]`/`[JsonObject]`/`[DataContract]`を持つか、
メンバーに`[Key]`/`[JsonProperty]`/`[SerializeField]`/`[SerializeReference]`/`[Option]`等が付いているか、
`Deserialize<T>`/`DeserializeObject<T>`/`FromJson<T>`のジェネリック引数に現れた型なら「シリアライズ型」とみなし、
**そのプロパティと引数なしコンストラクタを除外**する。
加えて`[Serializable]`型（メタデータの型フラグ。カスタム属性ではないので`TypeDefinition.IsSerializable`で見る）の
引数なしコンストラクタも除外する。Unityが`[SerializeField] EffectSettings[]`のような配列要素を復元するため。

### Mooresmaster（マスタデータ）は反射ではない

`mooresmaster.Generator`が吐く`XxxLoader.Load(JToken)`は `return new XxxModel(...)` を**リテラルで生成**するので、
モデル型のコンストラクタにはIL上の呼び出し元がある。反射用の除外は不要。
ただしモデル型自体が生成コードなので`GeneratedCode`で母集団から外れる。

### 属性スキャンによるハンドラ登録は存在しない

- `IPacketResponse`は`Server.Protocol/PacketResponseCreator.cs`のコンストラクタで
  `_packetResponseDictionary.Add(tag, new XxxProtocol(serviceProvider))` と**明示的に`new`**している。属性スキャンなし
- Web UIのアクションハンドラも`WebUiGameBinder.cs`で`hub.RegisterAction(new XxxActionHandler(...))`と明示的
- `SendMessage`/`BroadcastMessage`、`Invoke("...")`/`InvokeRepeating("...")`/`StartCoroutine("...")`、
  `MakeGenericType`/`MakeGenericMethod` は**リポジトリ内に1件も無い**ため、対応する除外規則は入れていない

### 文字列リフレクション（除外しきれない残り）

- `Mod.Loader/ModsResource.cs`: `Assembly.LoadFrom` + `Activator.CreateInstance` で外部Mod DLLの
  `MoorestechServerModEntryPoint`派生型を生成 → `Mod.Base`/`Mod.Config`をアセンブリ単位で除外
- `Server.Boot/Args/CliConvert.cs`: `[Option]`付きプロパティを`GetProperties`で走査 → `[Option]`をシリアライズ属性扱い
- `Client.Skit/.../SkitCommandExecutor.cs`: `GetField("CommandId")` で文字列参照（対象は生成コード側）

## 既知の限界（このリストは削除候補であって、自動削除してはならない）

以下はILに現れないので、**検出漏れ（本当は生きているのにリストに載る）が原理的に残る**。

- **UnityEventのシーン/Prefab配線**（`.prefab`/`.unity`の`m_MethodName`）。
  `Assets/Scripts`配下では0件だが`Assets/Dependencies/StarterAssets`に7件ある
- **`uloop execute-dynamic-code`とプレイテストシナリオ`.cs`からの呼び出し**。
  `.agents/skills/unity-playmode-recorded-playtest/scenarios/*.cs` が `PlaytestRunner.Run`や
  `PlaytestDriver`の公開APIを呼ぶが、これらはアセンブリ外のスニペットなのでIL走査に入らない
  （`Client.Playtest`はTest分類なので母集団には入らないが、そこから呼ばれるproductionメンバーの扱いには注意）
- **`GetMethod`/`GetProperty`/`GetField`等の文字列リフレクション**
- **`Activator.CreateInstance`による動的生成**
- **OneJS/JavaScript側からのバインディング呼び出し**

削除前に、対象メンバー名でリポジトリ全体（`.prefab`/`.unity`/シナリオ`.cs`/`.js`）をgrepして裏を取ること。

その他の限界:

- 過剰除外の方向に倒してある。interfaceのシグネチャ照合でジェネリック型引数が解決できない場合は
  ワイルドカード`*`に潰すため、同名同引数数のメンバーが巻き込まれて除外されることがある
- `typeof(X)`が出てくるだけでその型のコンストラクタを除外する。比較目的の`typeof`でも除外側に倒れる
- `Register`という一般的な名前をDI登録メソッドとして扱うため、DI以外の`Register<T>()`の
  ジェネリック引数型もコンストラクタ除外に倒れる
- 母集団はpublicメンバーのみ（internal宣言のメンバーは対象外。リスト3-Bは「publicをinternalへ」の提案であって
  既存internalの棚卸しではない）

### リスト4の限界

- **`const`はILに残らない**。`public const string EventTag = "..."` を他アセンブリが読んでも参照は0件に見える
  （コンパイル時に値がインライン化されるため）。`*EventPacket`のclient参照元が`-`なのはこれが理由で、
  「client側で本当に使われていない」ことの証明にはならない
- 型の使用箇所は**IL・メタデータに現れるものだけ**。属性のblob・`.prefab`のシリアライズ参照は数えない
- コンストラクタで購読を張るだけの「配線用シングルトン」は、設計として正しくても4-Bに載る（解決者が原理的に居ないため）。
  意図の裁定はverifierに任せている

### リスト5の限界

- `AttachExternalCancellation` / `WithCancellation` / `SuppressCancellationThrow` 経由でトークンを繋ぐ形は
  **未対応**（リポジトリに実例が0件のため実装していない。使い始めたら対応を足すこと）
- `CancellationToken.None`/`default`の検出は**CTが最後の引数である前提**で直前の命令列だけを見る。
  `Foo(CancellationToken.None, other)` のように前方に置かれた場合は検出できない
- 呼び出し元の「トークンを持っている」判定にラムダのクロージャが絡む場合、指摘は
  `<>c__DisplayClass…` の名前で出る。宣言場所（PDB由来の`path:line`）は正しいのでそちらで追うこと
- CTSの後始末判定は**メソッド単位**。同じメソッド内でフィールドを読みかつCancel/Disposeを呼んでいれば
  後始末済みとみなすので、別々のCTSフィールドを扱うメソッドでは取りこぼす方向に倒れる
- 自動実装プロパティのCTS（バッキングフィールドが`<X>k__BackingField`）は母集団に入らない
- **外部アセンブリの`Resolve()`は禁止**。ScriptAssembliesとPackageCacheには型フォワードの輪があり、
  Cecilの`ExportedType.Resolve()`が無限再帰してスタックオーバーフローする（`catch`できない）。
  引数・戻り値は`MethodReference`から直接読み、解決が要る判定はmoorestechアセンブリに限ること
