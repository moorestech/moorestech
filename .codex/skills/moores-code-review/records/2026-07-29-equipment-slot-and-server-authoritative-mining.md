# plan B 装備スロット新設＋採掘サーバ権威化 レビュー記録 (2026-07-29)

<!-- 1レビュー実行=1ファイル。命名: YYYY-MM-DD-<topic>.md（再レビューは -r2 付き新ファイル＋相互リンク1行）。
     記録は不変。マージ後に判明した事実のみ「事後結果」へ追記可。設計根拠: docs/superpowers/specs/2026-07-23-review-records-design.md -->

本記録は2巡を含む。1巡目＝5系統フル、2巡目＝修正の検証に絞った7観点。

## 対象
- **1巡目** base: `7a358b1a8` / reviewed head: `a5dc183e4`（plan B 実装8タスク・18コミット）
- **2巡目** base: `a5dc183e4` / reviewed head: `5edfb4898`（1巡目指摘＋ユーザー裁定への対応・7コミット）
- ブランチ: `tree2` / PR: 未作成
- 設計spec: `docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md`（判断台帳がSSOT）
- 実装計画: `docs/superpowers/plans/2026-07-28-equipment-slot-and-server-authoritative-mining.md`
- context要約
  - ゴール: 装備スロット（独立 `IOpenableInventory`・マスタ由来枠数）の新設／採掘のサーバ権威化（ダメージ算出＋`attackSpeed` クールダウン）／装備同期を3点セット標準で実装／Web UI に装備HUD／素手を `-1` として全層で扱う
  - 非目標: 後方互換・性能最適化・将来拡張性／ホットバーの廃止（plan C へ）／採掘の拒否理由をクライアントへ返すこと
  - 許容トレードオフ: クールダウン閾値 `attackSpeed × 0.9`（ジッタ余裕）／時刻源は `DateTime.UtcNow` 直参照・揮発Dictionary保持／`[Key(4)]` を欠番のまま残す／距離検証はしない（座標がクライアント申告値のため）
  - 制約: AGENTS.md 全般／設計原則（汎用基盤にドメイン語彙を持ち込まない・フォールバック禁止・変化検知は購読・前例に従う・3点セット）／マスタは4段階／webui-design ホワイトリスト

## 系統別判定

### 1巡目（`7a358b1a8..a5dc183e4`・5系統32観点）

| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0（新規） | 既存違反3件のみ（try-catch根拠コメント欠落1・デフォルト引数2）。plan B 以前から存在 |
| moores設計レンズ10本 | あり | **移動経路の全層欠落**を複数レンズが独立検出。`Core.Inventory` への受入制限基盤追加を前例違反と判定 |
| 汎用reviewer21本 | あり | 採掘中の装備切替による乖離／ホイールがHUD上で死ぬ／選択変更イベントの番兵／素通しプロパティ／ツール照合のテスト欠落 |
| Codex外部監査 | あり | 移動経路欠落を独立検出 |
| Fable全般 | あり | 同上 |

**1巡目の最重要所見（5系統一致）**: 装備スロットへアイテムを入れる経路がクライアント・ホスト・サーバーのどの層にも存在せず、**実プレイで採掘が一切成立しない**状態でマージ寸前だった。サーバーのパケットテストがプロトコルを直接叩き、Web e2e が TypeScript モックホストを相手にするため、実結線の欠落を全テストが素通りさせていた。

### 2巡目（`a5dc183e4..5edfb4898`・7観点）

| 系統 | Critical | 要旨 |
|---|---|---|
| precedent-alignment（レンズ） | なし | `Core.Inventory` への基盤追加は master とバイト一致まで復元済み。新設パターンは全て前例に載る。Warning 4件 |
| domain-boundary（レンズ） | なし | 撤去は是正方向。Warning 3件（毎tickポーリング／state間依存／素通しプロパティ） |
| core-any-test-mutation-effectiveness | **あり** | **1巡目の穴が再発しても全テストが緑のまま通る**（mutation 2件を物理実証）。C#層3箇所が無テスト |
| core-cs-dead-code-and-scope | あり | 撤去の網羅性は確認済み（残存参照0）。`InsertionItemBySlot` の使われないデフォルト引数 |
| core-any-user-intent-fulfillment | なし | 依頼11件すべて達成根拠あり。Warning 3件（クロスヘアのpointer-events／spec本文未更新／素通しプロパティ） |
| core-ts_tsx-react-antipattern | なし | 構造的違反なし。Warning 5件（クリック可否ゲートが広すぎる／範囲外TypeError／全幅バンドの素通し 他） |
| Fable全般・Codex | 2巡目は未実施 | 修正の検証に絞ったため観点を限定 |

**2巡目の最重要所見**: 1巡目で見つけた最悪の欠陥を直したにもかかわらず、**同じ症状の退行を検知する手段が無い**ことが mutation で実証された（`InventoryMoveServerDispatcher` の Equipment 分岐を Grab に書き換えても全テスト緑）。

## 適用した修正

### 1巡目指摘への対応（`570e54a7b`〜`5edfb4898`）
- 採掘中の装備切替による乖離の検知（reviewer） → `570e54a7b`
- 装備インベントリの受入制限機構を全撤去（ユーザー裁定） → `1052505c5`
- 装備へのアイテム移動経路を全層に新設＋クリックを移動へ統一（5系統一致・ユーザー裁定） → `c3990b4bd`
- 装備枠縮小時のあふれ装備をメインへ退避しアイテム消失を防止（reviewer） → `5edfb4898`
- spec 判断台帳の更新 → `e2383e0df`

### 2巡目指摘への対応
- spec **本文**の更新（撤去済み機構が現役として残存・2系統一致） → `54df17ccd`
- grab 成立条件を単一述語 `screenAllowsGrab` へ共通化（react W1・ユーザー裁定） → Web UI 修正ウェーブ
- クロスヘア他の常時表示HUDのホイール素通し（user-intent W1 / react W5・2系統一致） → 同上
- `slotActions` の範囲外 TypeError ガード（react W2） → 同上
- 移動経路C#層3箇所へのテスト追加（test-mutation Critical） → C# 修正ウェーブ
- `EquipmentInventoryData : ISortExcludedSlots`（precedent W1） → 同上
- `InsertionItemBySlot` のデフォルト引数削除（dead-code Critical） → 同上
- セーブ復元オーバーフローの検査復活＋メイン満杯テスト（2系統一致） → 同上
- 素通しプロパティ廃止・`ResolveUsableTool` の移設・毎tickマスタ走査の解消 → 同上

## 設計判断（AskUserQuestion裁定）

- Q: 装備へのアイテム移動経路をどこで実装するか / 裁定: **plan B 内で実装する** / 適用: `c3990b4bd`
- Q: 装備枠のクリックに「選択」と「移動」の両方を持たせるか / 裁定: **クリックは常に移動、選択はホイール専用** / 適用: `c3990b4bd`
- Q: 選択インデックスの楽観更新と同値時送信抑止 / 裁定: **楽観更新を維持し、送信抑止だけ外す**（一度サーバとズレると同値再送が握り潰されて恒久的にズレるため） / 適用: `c3990b4bd`
- Q: 受入制限（ツール限定・1枠1個）をどう強制するか / 裁定: **一旦受け入れ制限自体をやめる。現状は実害が無い。無用な複雑性を課すオーバーエンジニアリングになるので計画を変更する** / 適用: `1052505c5` で機構ごと撤去。ツール限定と1枠1個の両方を撤回
- Q: 常時表示HUDのクリック可否ゲートを `GrabOverlay` の描画条件に合わせるか、逆か / 裁定: **どちらかに寄せるのではなく「ロジックをgrab itemの表示と完全に共通化」する** / 適用: `screenAllowsGrab` を単一の正とし、両者がそれだけを読む。App から画面名リテラルを排除

## 破棄した指摘
- `Attack(int.MaxValue)` によるHPアンダーフロー疑い（オーケストレータ発） — `Destroy()` が `CurrentHp = 0` を設定するため `[0, hp]` を保つ。レビュアーの反証を受け撤回
- `ToolMaster.Initialize()` の空実装（dead-code W1） — `IMasterValidator` が実装を要求するため不可避（precedent が Info 判定）
- `items.yml` の `tools` 配列と `ToolMaster.Tools`/`All` のデッドデータ化 — ユーザー裁定「一旦残置」により免責（suppressed 計3件）

## オーケストレータの判断誤り（記録として残す）

1. **ソート除外を「到達経路が無い」として不要と裁定したのは誤り。** `SortInventoryProtocol.cs:33` は identifier を無条件に解決するため、Equipment identifier を投げれば装備は実際に整理される。クライアントが送らないことを根拠にサーバー側の防御を省いており、サーバー権威化を進めているブランチとして矛盾していた。さらに役割同型の前例 `ISortExcludedSlots` が既に存在するのに採用しなかった。precedent-alignment レンズが検出
2. **Task 6 のブリーフでデバッグフラグの読み出しを `Game.Map` 内に指示した** — `Game.*` アセンブリが `Common.Debug` を参照する初の事例を作りかけた。タスクレビュアーが検出し、プロトコル層へ移設（前例 `PlaceBlockProtocol.cs:47`）
3. **破壊済みガードの falsification 指示が不十分だった** — プロトコル層のガードが先に効くためサービス層の穴を反証できなかった。fixer が2本目のテストを追加して補正
4. **spec の判断台帳だけ更新し本文を直さなかった** — 2系統が「同一文書内で現行実装と矛盾する記述が生きている」と検出。`54df17ccd` で修正
5. **検証フェーズを直列化せずサブエージェントを並行させ、Unity のコンパイルを衝突させた**

## 事後結果（マージ後追記可）

## メタ
- セッションID: `a10f3e5d-7ad8-4973-bb09-d038bd24e518`
- スキップ系統: 2巡目は Codex外部監査・Fable全般・決定論以外の reviewer 群を省略（修正の検証に観点を絞ったため）
- 備考: **実プレイ検証を実施**（`unity-playmode-recorded-playtest` / tutorial-pebble-challenge）。装備→伐採→原木3個→チャレンジ完了まで全アサートPASS、録画あり。「テストは全部緑なのに実プレイでは動かない」を潰す唯一の手段として機能した
- 備考: **Playwright e2e は C# ホストコードを守れない**ことを falsification で実測（`InventoryAreaMapper` の equipment 分岐を削除しても e2e 8/8 緑・C# テスト4件のみ RED）
