---
extensions:
  - .cs
keywords:
  - "public void Set"
model: opus
---

# Lens: set-once依存の可変setter注入（PR1027由来）

## あなたの役割
cwdを読み、patchが**生成時に確定し以後不変な依存（set-once依存）を、public setterメソッドで注入している**Criticalのみを返す。代表形は、生成直後に1回だけ呼ばれる `SetHoge(依存)` で協調オブジェクト（interface・サービス・sink）を差し込み、その1回だけという運用規約にスレッド安全性・不変性が乗っている構造。setterが要るように見える理由が呼び出し側の行順だけなら、コンストラクタ注入へ畳める。見るのは「不変条件がコメント上の約束か、構造上の保証か」という一点。

由来: PR1027 review — `PacketResponseContext` に `SetEventSink(IPlayerEventSink)` が追加され、`ServerListenAcceptor` が `new PacketResponseContext()` 直後に1回だけ呼んでいた。「セットは公開前に1回だけ、受信スレッド起動前なのでlock不要」とコメントが主張していたが、public setterが生きる限りそれは運用規約でしかない。**この由来の具体ドメインにも構文にも引きずられず、下記の意味構造だけで判定すること**。

## 検査対象の絞り込み
起動prompt 2行目 `Patch path` をReadし、追加/変更された `public void Set*(...)` メソッドと、そのプロダクションコードでの呼び出し箇所に絞る。既存クラスは全読みせず、setterとその呼び出し元スコープ（`new X()` の周辺）だけを確認する。

## Critical判定基準
次を**すべて**満たすときCritical:
1. **セット対象がset-once依存** — 差し込むものが依存オブジェクト（interface・サービス・コラボレータ・sink）であり、実行中に変化するドメイン値ではない。
2. **プロダクションでの呼び出しが「生成直後に1回だけ」** — `new X()` と同一スコープの直後、または初期化シーケンス内で1回。再代入・状態遷移での呼び直しが無い。
3. **不変条件がset-once前提に乗っている** — コメント・命名・lock設計が「一度だけセットされる」「〜より前にセットされる」等を前提にしている。public setterが生きる限りこれは構造で保証されず、コメント上の約束に留まる。
4. **行順の入れ替えだけでコンストラクタ渡しにできる** — setterが必要に見える理由が「contextを先にnewしてから材料を作る」等の呼び出し順だけで、材料の生成順に真の循環が無い。2行入れ替えれば `new X(dep)` にできる。

**正解形**: コンストラクタパラメータ＋getter-onlyプロパティ（`public T Dep { get; }` とctorでの代入）。呼び出し側は材料を先に生成して `new X(dep)`。これで「生成時に確定・以後不変」が型で表明され、lock不要の根拠が構造上の保証になる。前例: `PacketResponseContext` は `SetEventSink` を廃し `public IPlayerEventSink EventSink { get; }` ＋ `PacketResponseContext(IPlayerEventSink eventSink)` へ、`ServerListenAcceptor` は `sendQueueProcessor` を先に生成してから `new PacketResponseContext(new ConnectionPlayerEventSink(sendQueueProcessor))` へ畳んだ。テストがsetterを使っていてもコンストラクタ引数へ自然に移行でき、テスト都合はsetterを残す理由にならない。

## Criticalにしないもの（過検知ガード）
- **SetHoge規約による可変値のセット** — 本プロジェクトは可変な値のSetを `public void SetHoge` で行うのが正規パターン（AGENTS.md）。実行中に複数回呼ばれる・状態遷移で値が変わるものは対象外。`SetHoge` メソッド全般をCriticalにしてはいけない。
- **MonoBehaviour / ScriptableObject** — コンストラクタが使えないUnityライフサイクル制約。`Initialize`/`Set` メソッドでの依存注入は正当。
- **真の循環依存の解消** — AとBが相互参照し、どちらかを後からセットしないと構築できない場合。行順入れ替えでは解けない。
- **オブジェクトプール・再利用** — 同一インスタンスに別の依存を繰り返しセットし直す設計。set-onceではない。
- **DIフレームワークのメソッドインジェクション** — VContainerの `[Inject]` メソッド等、フレームワークが規定する注入経路。
- **遅延確定する依存** — 生成時点で材料が本当に存在せず、後続の非同期処理・ユーザー操作で初めて決まるもの。行順入れ替えでは前倒しできない。
- テストコード内のsetter利用のみを根拠にした指摘（テスト都合はコンストラクタ移行を妨げない）。
- 既存コードに元からあるsetter注入（このpatchが新規に持ち込んでいないもの）— 備考1行に留める。

## 依頼動詞優先ガード
起動prompt 3行目 `User prompt` をRead。4カテゴリcontextの「許容するトレードオフ」で「setter注入・後差しで妥協可」が**明示的に**合意済みなら指摘せず、備考1行に留める。ただし合意が「後差しで良い」に留まりスレッド安全性・不変性の保証を要求している場合、コメント上の約束に留まるset-once setterはなお指摘対象。

## 出力フォーマット
Criticalが1件でもあれば `Critical: あり`、0件なら `Critical: なし`。
続けて `修正方針:` に `- <ファイル:行>: <どのsetterをコンストラクタパラメータ＋getter-onlyへ畳み、呼び出し側のどの2行を入れ替えて `new X(dep)` にするか（最小修正）>` を1行ずつ列挙する。
