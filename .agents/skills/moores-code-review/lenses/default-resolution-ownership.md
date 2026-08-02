---
extensions:
  - .cs
keywords:
  - "Default"
model: opus
---

# Lens: デフォルト値解決の責務漏れ（PR1108/1109由来）

## あなたの役割
cwdを読み、patchが**「未指定ならデフォルト」の解決を値の所有者の外へ漏らしている**Criticalのみを返す。デフォルト値の定義と省略時解決は、その値を消費するコンポーネントの内部に1箇所だけ置く。省略可能性はnullable（`int?` 等）のまま所有者のAPIまで素通しする、が本プロジェクトの正解形。

由来: PR1108/1109 手動修正 — 動的ポートバインド導入時、`ServerListenAcceptor` が `public const int DefaultPort = 11564` を公開し、呼び出し側 `ServerInstanceManager` が `settings.Port ?? ServerListenAcceptor.DefaultPort` とデフォルト解決していた。修正は `DefaultPort` をprivateへ戻し、`CreateBoundListener(int? argPort)` がnullableを受けて内部で `var port = argPort ?? DefaultPort;` と一箇所解決する形。フォレンジック・リプレイでは15系統全てがこの形を素通しし、複数系統が漏れ形をむしろ「達成根拠」として肯定引用した（`?? Default` は一見自然に読めるため、意識して疑わない限り指摘に上がらない）。**この由来の具体ドメイン（ポート・サーバー起動）にも構文にも引きずられず、下記の意味構造だけで判定すること**。

## 検査対象の絞り込み
起動prompt 2行目 `Patch path` をReadし、追加行のうち次に絞る: (a) 名前に `Default` を含むpublic定数・staticフィールドの新設またはpublic化、(b) `?? 他型.Default*` など他コンポーネントの定数を参照する省略時解決、(c) 「未指定」を表すnullable値が所有者に届く前の中間層で非nullableへ潰されている箇所。

## Critical判定基準
次のいずれかでCritical:
1. **デフォルト値の公開＋呼び出し側解決** — コンポーネントが自分用のデフォルト値（`Default*` 定数等）をpublicで公開し、呼び出し側が `?? X.DefaultY` や条件分岐でそれを参照して省略時補完を行っている。その定数の役割が「省略時の補完」だけなら、所有者の外に見える必要がない。
2. **同一デフォルト規則の複数実装** — 「未指定なら値V」という同じ規則（同じ値・同じ意図）が複数箇所で独立に書かれている。1箇所（所有者）に寄せ、他はnullableを素通しする。
3. **省略可能性の早期潰し** — 設定・引数が `int?` 等で「未指定」を表現しているのに、所有者のAPIが非nullableのままで、中間層が `?? Default` で既定値を焼き込んでから渡している。APIシグネチャをnullableにして省略可能性を所有者まで運ぶ。

**正解形**: 所有者に `private const T DefaultX = ...;` を置き、public APIは `T?` を受けて先頭で `var x = argX ?? DefaultX;` と1回だけ解決。呼び出し側は `settings.X` を素通し。前例: `ServerListenAcceptor.CreateBoundListener(int? argPort)`（`Server.Boot/Loop/ServerListenAcceptor.cs`）。C#注意点: nullableパラメータへ `??=` して以降non-null前提で使う形はコンパイルエラーの温床なので、非nullableローカルへ `var x = argX ?? DefaultX;` と受ける（PR1109がこの修正）。

## Criticalにしないもの（過検知ガード）
- **所有者内部のprivateデフォルト解決** — 同一クラス内の `?? Default*` は正解形そのもの。
- **呼び出し側が自分のポリシーとして別の明示値を選ぶこと** — 例: クライアントが `Port ??= 0`（OS自動採番）を注入するのは所有者のデフォルトの再実装ではなく、その層の意図的な決定。所有者のデフォルト値・定数を参照/複製している場合だけが対象。
- **マスタデータの `?? Default`** — フォールバック自体が禁止の領域（master-data-defenseレンズ・決定論master_default_fallbackの領分）。本レンズは実行時設定・起動引数など省略が正当な値のみ扱う。
- **省略時補完以外の役割を持つ公開定数** — 例: 接続プローブ先として双方が知る必要のある既知ポート。ただし同じ値の公開定義が2本になっている場合は備考1行（重複統一はprecedent-alignmentの領分）。
- C#のデフォルト引数（`= 値`）— 決定論チェック（デフォルト引数禁止）の領分。
- テストコード内の明示値渡し。
- 既存コードに元からある違反のうち、このpatchが触っていないファイルのもの — 備考1行に留める。**このpatchが編集中のファイル内の既存違反はWarningで必ず返す**。

## 依頼動詞優先ガード
起動prompt 3行目 `User prompt` をRead。「許容するトレードオフ」「非目標」に合致する指摘は**破棄せず**、`suppressed-by: <トレードオフ1行, 出所ラベル>` を付けて**重大度そのまま**で返す（統合側が報告の「免責で消された指摘」節に載せる）。suppressed化できるのは出所が `[ユーザー裁定: ...]` / `[ADR: ...]` の行だけ。`[agent前提]` またはラベル無しの行は免責事由にならない（通常のCritical/Warningとして返す）。

## 出力フォーマット
Criticalが1件でもあれば `Critical: あり`、0件なら `Critical: なし`。
続けて `修正方針:` に `- <ファイル:行>: <どの定数をprivate化し、どのAPIをnullable化して、解決をどこへ一本化するか（最小修正）>` を1行ずつ列挙する。
