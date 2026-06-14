# UI 網羅性 再監査 実行計画書

**由来**: 学習メモ `ui-completeness-off-state-overlays.md`（uGUI→Web 移行で `BackgroundSkitManager` を見落とした失敗）を、再発防止のための**実行可能な再監査手順**へ展開したもの。
**対象成果物**: `docs/cef-webui-migration-todo.md`（特に §5 機能カバレッジ・チェックリスト）の網羅性を、構造的に保証されたレベルへ引き上げる。
**作成日**: 2026-06-14

---

## 0. 背景・目的

### 何が起きたか
uGUI の「全 UI 機能洗い出し」で `Client.Game/InGame/BackgroundSkit/BackgroundSkitManager`（GameScreen 中のオーバーレイ会話）を見落とし、ユーザー指摘で発覚した。再監査エージェントも検知できなかった。

### なぜ重要か
移行 TODO の価値は**網羅性の保証**にある。1 個見落とすと「全機能を洗い出した」という前提自体が崩れ、後続の Phase 計画・カットオーバー判定（D6）の基礎が揺らぐ。チェックリストが「埋まっている」ことと「正しい」ことは別問題。

### このドキュメントのゴール
- 移行 TODO の UI 網羅性を**棚卸し軸の置換**（状態機械→ディレクトリ走査）によって構造的に再保証する、再現可能な手順を定義する。
- 残存する見落とし（特に「状態を持たないオーバーレイ／常時表示／プレイ中割り込み UI」）を炙り出し、TODO へ反映する。
- 監査を「名前の存在」でなく「責務の実コード検証」で確定する規律を手順に組み込む。

### 非ゴール
- サーバー（C# ゲームロジック）側の機能棚卸し。
- 各 FEAT の詳細設計・API スキーマ。
- 移行実装そのもの（本書は監査計画であって実装計画ではない）。

---

## 1. スコープ

### 監査対象（クライアント UI 表層の全域）
`moorestech_client/Assets/Scripts` 配下のうち、UI を駆動・出力する全クラス。**監査ソースルート（固定リスト。Phase 1 はこの全件を機械列挙し、§5 受け入れ条件はこの全件被覆を要求する）**:

| ソースルート | 種別 | 備考 |
|---|---|---|
| `Client.Game/`（**`Common`/`InGame`/`Skit` 全サブツリー**） | asmdef | 主対象。**`InGame/` だけに絞らない**: 兄弟の `Client.Game/Common/`（`GameStateController`・`UIRaycastTarget`）と `Client.Game/Skit/`（`SkitManager` = 全画面スキット活性化ドライバ。BackgroundSkitManager と同型）も UI ドライバを含む。 |
| `Client.Skit/` | asmdef | 全画面スキットの View/Command（`SkitUI`・`BackgroundSkitUI`）。 |
| `Client.MainMenu/` | dir（**非 asmdef**。`Client.Game` 配下でなく predefined Assembly-CSharp に属す top-level 兄弟） | 別シーン UI。 |
| `Client.DebugSystem/` | dir（**非 asmdef**。predefined Assembly-CSharp） | `ItemGetDebugSheet : DefaultDebugPageBase` 等、**カスタム基底経由**の UI を含む。 |
| `Client.CutScene/` | asmdef | ムービー UI。 |
| `Client.Localization/` | asmdef | `TextMeshProLocalize.cs` / `Localize.cs`（実在の text 書換 UI ドライバ）。 |
| `Client.Common/` | asmdef | 共通 UI 部品があれば。 |
| `Client.Starter/` | asmdef | `InitializeScenePipeline` 等のローディング/初期化 UI。 |

> **注（事実）**: `Client.MainMenu`・`Client.DebugSystem` は `.asmdef` を持たない単なるソースルートで独立アセンブリではない。「アセンブリ」と「ソースルート」を混同しない。実在 asmdef は Client.Game/Common/CutScene/Localization/Skit/Input/Mod/Network/Starter/Tests/WebUiHost。
>
> **母集団から外す top-level とその根拠（暗黙除外をしない。台帳0 で機械列挙して分類）**: `Client.Input`（InputSettings・入力 Composite のみ、画面出力なし）、`Client.Network`（通信層）、`Client.Mod`（Mod ロード）、`Client.WebUiHost`（**移行先**の Web ホスト。プレイヤー向け uGUI を出さない）、`Client.Tests`（テスト）、`Editor`（editor 専用ツール・Inspector・BuildPipeline。player UI 非産出）。各々「UI 非産出」を Read で確認したうえで除外する（除外も監査結果として記録）。
>
> **UI 定義資産レイヤー（`.cs` 以外。uGUI ゲームでは prefab が主層）**: 画面構造は `.cs` でなく **prefab/uxml/uss** にしか無いことが多い（実在: `Assets/Asset/UI/Prefab/` に `MainGameUI`・`ContextMenu`・`MouseCursorTooltip`・`MissionBar`・`HudArrow` 等 30 個の uGUI Canvas prefab + `Skit/SkitUI.uxml`/`.uss`。`SkitUI.cs` は `Q<T>("MainText")` 等**文字列名でしか**要素を引かない）。これは `Assets/Scripts` の外にあり二重に漏れる。**資産台帳C（prefab/uxml/uss）を第2母集団として別途列挙し**（§3 Phase 1）、各 (I) クラスが Addressables/`[SerializeField]` で参照する資産を Web 移行対象として紐づける。uxml は prefab の `UIDocument.sourceAsset` 経由でしか `.cs` に繋がらない場合があるため、厳密には Unity 依存グラフで双方向走査する。

### 明示的に「状態外」を含める
`UIStateEnum`（11 状態: GameScreen / PlayerInventory / SubInventory / PauseMenu / DeleteBar / Story / PlaceBlock / ChallengeList / ResearchTree / Debug / TrainHUDScreen）に**ひもづかない** UI（オーバーレイ・常時表示・割り込み）を独立カテゴリとして必ず探索する。

---

## 2. 根本原因 → 是正原則

失敗は 3 つの独立した穴が重なって発生した。各々に恒久的な是正原則を割り当てる。

| # | 根本原因 | 是正原則（本手順に組み込む） |
|---|---|---|
| 1 | 探索スコープを `UI/` 配下 + `Client.Skit/` に限定。`BackgroundSkit/` は `UI/` の兄弟で隙間に落ちた。 | **P1: 背骨をディレクトリ走査に置く。** `InGame/` 直下の UI/ 以外の全ディレクトリを機械的に列挙し、1 つずつ「UI を出すか」を判定する。状態機械は補助軸に降格。 |
| 2 | 網羅レンズの背骨を `UIStateEnum`(11 状態) に固定。固有 state を持たないオーバーレイは状態軸では構造上拾えない。 | **P2: 「状態外 UI」を独立カテゴリとして明示探索する。** オーバーレイ／常時表示／プレイ中割り込みを専用チェック項目にする。 |
| 3 | 計画書に `BackgroundSkitUI` の名前が一度あったため、再監査が「名前がある＝カバー済み」と誤判定（文字列存在 ≠ 正しい分類）。 | **P3: 名前一致を被覆と認めない。** チェックリストの各項目は、責務が実コードと一致するかを必ず Read で検証してからチェックを付ける。 |

---

## 3. 実行フェーズ

> 各フェーズは前フェーズの出力を入力にする直列パイプライン（**ただし Phase 5 で収束まで反復ループ**。詳細は §5/§6）。Phase 0〜4 は read-only（コード変更なし）。Phase 5 のみ TODO 文書を更新する。
>
> **本手順の核（レビューで是正した最重要点）**: **網羅の母集団は「ソースルートの全 `.cs` ファイル台帳」であり、grep ヒットではない。** grep をふるいにすると、ヒット 0 のディレクトリ・カスタム基底経由の UI・トークンを持たない子 View が**無検査で消える**（＝ BackgroundSkit を生んだ「沈黙する盲点」の再現）。そこで **grep は「優先順位付け（UI らしさのフラグ）」専用に降格**し、台帳の全件 triage を網羅の根拠にする。

### Phase 0: 棚卸し軸の確定（背骨の置換）
**目的**: 状態機械を背骨から外し、母集団を全ファイル台帳に固定したうえで、確認軸を多重化する。

確認軸（母集団に対する**確認の視点**であって、母集団そのものではない）:
- **軸 A（構造軸 / 主軸）**: ソースルートの全 `.cs` 台帳 + ディレクトリ台帳。**これが網羅の母集団**。
- **軸 B（状態軸 / 補助）**: `UIStateEnum` 11 状態（**既存 TODO §5 の軸**＝本書 §5 ではない）。「状態に結びつく UI」だけを保証する。
- **軸 C（オーバーレイ軸 / 盲点専用）**: 状態に結びつかない UI（常時表示・割り込み・GameScreen 上オーバーレイ）。

> **軸 A/C が共有する前提の明記**: 軸 A（dir/ファイル台帳）も軸 C（オーバーレイ探索）も「UI コードが特定ソースルート内にある」前提に立つ。**両軸はディレクトリ位置に依存し独立でない**。したがって母集団は「§1 の全ソースルートの全 `.cs`」とし、ディレクトリ名・クラス名・grep トークンによる事前選別は一切しない（選別は triage で実コードを Read してから）。

**完了条件**: 3 軸の定義と「各軸が保証する範囲・取りこぼす範囲」を明文化。母集団＝全ファイル台帳であることを宣言。

### Phase 1: 母集団台帳の生成（機械列挙）+ 優先順位フラグ
**目的**: 人間の記憶にも grep にも頼らず、**§1 全ソースルートの全 `.cs` を台帳化**する。grep はその台帳に「UI らしさ」フラグを付けるだけ。

実行コマンド（`moorestech_client/Assets/Scripts` で実行。**ルートは手書きせず機械列挙→分類で導出**＝「固定リスト＝沈黙する隙間」の再帰を断つ。`roots` 配列は**台帳0 の『対象』分類の結果**であって人が選んだものではない。zsh/bash 両対応のため文字列でなく配列）:
```bash
# (台帳0) ソースルートの全数列挙＋分類（← roots を導出する根拠。手書きリストにしない）
#   Assets/Scripts 直下の全ディレクトリ と 全 .asmdef を列挙し、各行を 対象/除外(根拠)/Editor専用 に分類。
find . -mindepth 1 -maxdepth 1 -type d | sort                 # 全直下ディレクトリ（新規/改名もここに必ず出る）
find . -name "*.asmdef" | sort                                # 全 asmdef（暗黙ルートの取りこぼし検出）
#   → 分類表を作り「対象」に入れた行だけを下記 roots に写す。除外行には UI 非産出の根拠を必須記入。

# roots = 台帳0 の「対象」分類（例。実行時は台帳0 の分類結果で確定。Client.Game は InGame に絞らず Common/Skit も含む）
roots=(Client.Game Client.Skit Client.MainMenu Client.DebugSystem \
  Client.CutScene Client.Localization Client.Common Client.Starter)

# (台帳A) ディレクトリ台帳（triage で各行に判定必須）。Client.Game 直下(Common/InGame/Skit) + その直下
find Client.Game -mindepth 1 -maxdepth 2 -type d | sort   # ※ stderr は抑止しない（欠落ルートを沈黙させない）

# (台帳B) 全ソースルートの全 .cs（＝網羅の主母集団。これを triage する）
find "${roots[@]}" -name "*.cs" | sort

# (台帳C) UI 定義資産（第2母集団。.cs 限定の穴を塞ぐ）。uGUI ゲームなので **prefab が主たる UI 定義層**（uxml は僅少）。
# cwd=Assets/Scripts なので資産ツリーは姉妹の ../Asset。各資産を (I) の .cs ドライバ（Addressables/SerializeField 参照元）に紐づける。
find ../Asset . \( -name "*.uxml" -o -name "*.uss" -o \( -path "*/UI/*" -name "*.prefab" \) \) | sort
# ※ より厳密には Unity 依存グラフ（AssetDatabase.GetDependencies）で .cs↔prefab↔scene↔uxml を双方向走査する（uxml は prefab の UIDocument.sourceAsset 経由でしか .cs に繋がらない場合がある）。

# (フラグ1) UI らしさ: UI ウィジェット/トークン（優先順位付け専用。漏れても台帳Bが母集団）
grep -rlE "UIDocument|TextMeshProUGUI|TMP_Text|RawImage|VisualElement|Canvas|Button|Slider|InputField|UICursor|Tooltip|Hud|Overlay" \
  --include="*.cs" "${roots[@]}" | sort

# (フラグ2) 状態外で表示制御する疑い（割り込み/常時表示のヒント。BackgroundSkit/SkitManager はここで光る）
grep -rlE "WaitUntil\(.*CurrentState|SetActive|DebugPageBase|OnLanguageChanged" \
  --include="*.cs" "${roots[@]}" | sort
```

**出力**: 台帳0（全ルート分類表）、台帳A（dir 台帳）、台帳B（全 `.cs` 主母集団）、台帳C（UI 資産 = prefab/uxml/uss、第2母集団）、各ファイルへの UI らしさフラグ。
**完了条件**: 台帳0 で全直下ディレクトリ・全 asmdef が「対象/除外/Editor」に分類済み（roots はその『対象』と一致）。台帳A/B/C が生成され、台帳B の各ファイルに「フラグ有無」が付いている。**フラグ 0 のファイルも台帳B に残る**（捨てない）。`find` の stderr は抑止しない（`2>/dev/null` を足すとルート typo で母集団が黙って縮む）。

> **注**: grep（フラグ1/2）は 3D オブジェクト・状態プロセッサ等の非 UI も拾い、逆に `ItemGetDebugSheet : DefaultDebugPageBase` のようなカスタム基底経由 UI・トークンを持たない子 View（`BackgroundSkitUI`）・別ディレクトリの活性化ドライバ（`Client.Game/Skit/SkitManager`）を**取りこぼす**。だからこそ grep を母集団にせず、フラグ 0 のファイルも台帳B に残して Phase 2 で Read する。**漏れ 0 の担保は台帳B の全件 triage であって grep ではない**。

### Phase 2: トリアージ（台帳B 全件 → 3 分類）
**目的**: **台帳B の全 `.cs`**（grep ヒットだけではない）を実コード Read で分類し、UI のみ残す。フラグ有りを優先的に、フラグ 0 も dir 台帳の網羅確認として一巡する。

分類の単位は**ファイル**。ただし**1 ファイルに複数クラスがあり責務が割れる**場合、件数突合（ファイル単位）が情報落ちを検出できないため、(I) を含むファイルは**クラス/責務単位の小台帳**に展開して Phase 4 へ渡す（ファイル台帳＝網羅証明、クラス台帳＝責務突合の入力）。

- **(I) プレイヤー向け UI 出力** — 画面に情報/操作を出す。→ **監査対象。Phase 4 へ。**
- **(II) 3D ワールドオブジェクト / プレビュー / VFX** — `MapObjectHpBarView` 等の「3D 連動 UI」は **(I) と (II) の境界**。表示が画面 UI なら (I)、純粋な 3D メッシュ/プレビュー（`BlockPreviewObject`, `BezierRailMesh` 等）は (II)。
- **(III) 内部ロジック / データストア / 状態プロセッサ** — UI を出さない（`*StateChangeProcessor`, `*Datastore`, `BlockGameObject` 等）。→ **対象外。**

**追跡ステップ（子 View / 活性化ドライバの取りこぼし防止）**:
- (I) と判定したクラスの `[SerializeField]`/フィールドが指す `*UI`/`*View` 型を辿り、grep が直接拾っていなくても候補（(I)）に加える（`BackgroundSkitManager → BackgroundSkitUI`、`GameStateController → CurrentChallengeHudView/HotBarView`）。
- (I) の View が `UIDocument`/`VisualTreeAsset`/prefab を参照する場合、**台帳C（uxml/uss/prefab）の該当資産を Web 移行対象として紐づける**（`SkitUI.cs → SkitUI.uxml`）。資産が `.cs` ドライバ無しで存在しないか台帳C 側からも逆確認。

**dir 台帳の消化**: 台帳A の全 dir に「triage 完了。UI 0 件なら『Read 済・UI 無し』と根拠記入」を付ける。**ファイル 0 ヒットでも『見ていない』にしない**。

**境界ルール（P3 の予防）**: 迷ったら「画面にピクセルを出すか」で判定し、Read した実コードの根拠（クラス名でなく中身）をメモする。

**出力**: 分類表（ファイル → I/II/III → 根拠 1 行）＋ (I) のクラス/責務小台帳 ＋ dir 台帳（全行に完了印）＋ 台帳C 紐づけ結果。
**完了条件**: 台帳B の**全件**が 3 分類のいずれかに割り当てられ（件数突合: triage 件数 = 台帳B 件数）、台帳A の全 dir に完了印、台帳C の各資産が母集団内 `.cs` に紐づくか確認済み。(I) の確定リスト（クラス単位）ができる。

### Phase 3: 状態外オーバーレイの明示探索（軸 C / 盲点専用）
**目的**: BackgroundSkit と同型の「状態を持たない UI」を構造的に探す（P2）。**名前による事前選別はしない**（P3）。

**軸 C の母集団 = 台帳B 全件**（grep ヒットや `Hud`/`Overlay` 名の和集合ではない）。漏れ 0 の担保は下記 step 1 の全件走査にあり、フラグ2 や名前は**優先順位付けのみ**に使う（候補集合の境界にしない＝§3 冒頭の「grep を母集団にしない」原則と統一）。

探索手順:
1. **台帳B の全 `.cs`（= Phase 2 で Read 済の母集団）に「状態遷移を伴わず画面に出るか」の軸 C 判定欄を 1 列足す**。dir 名・クラス名・grep トークンによる事前選別は**禁止**（「UIState 対応名を持たない dir だけ開く」式の名前照合は P3 違反）。
2. 優先順位フラグ（フラグ2 ヒット / 名前に `Hud`/`Overlay` を含む / `UI/` 配下 HUD 例 `CurrentChallengeHudView`）は**先に見る順番**を決めるだけで、判定対象は step 1 の全件。
3. 典型パターンを特に確認: 「常時表示」（`Update`/`OnEnable` で出続ける）、「割り込み」（`WaitUntil(... CurrentState == GameScreen)` や event 購読で**状態遷移なしに**出る。BackgroundSkit がこの型）、Tutorial の `HudArrow`/`MapObjectPin`。
4. **発火経路の推移閉包追跡**: 見つかった状態外 UI が別の UI（manager→別 UI 等）を起動しないか、発火元・発火先を**新規 UI が増えなくなるまで反復**して辿る（「1 段」で止めない＝多段連鎖 UI→manager→別 UI を取りこぼさない）。

**出力**: 状態外 UI の一覧（各々「どの瞬間に出るか / どの state にも属さない理由 / 発火元」付き）。
**完了条件**: 軸 C のリストが確定し、各項目が TODO に存在するか未反映かが判明する。

### Phase 4: TODO 突合（責務検証つき・双方向）
**目的**: Phase 2(I) + Phase 3 の確定 UI を `cef-webui-migration-todo.md` の FEAT/§5 と突合し、被覆を**責務レベル**で検証する（P3）。順方向（UI→FEAT）と逆方向（TODO の否定的主張→実コード反証）の両方を見る。

各 UI について:
1. **責務一致で FEAT を引く（名前検索でなく）**: この UI の責務を担う FEAT を **TODO §2 の全 FEAT 本体 + §5 チェックリスト**を走査して**実在し責務一致するか**確認（FEAT 定義は §2、§5 は被覆チェックリスト）。名前が出てくるだけ・別 FEAT に責務が紛れているだけ（例: BackgroundSkit が SKIT-1 に埋もれていた）は**不在扱い**。
2. 責務一致の FEAT があれば「一致」。記述が実コードとズレていれば「誤分類」。
3. 担う FEAT が無ければ「未反映」。
4. §5 チェックリストの各 `[x]` について、指す FEAT が実在し責務が正しいかを確認。
5. **逆方向チェック（網羅の盲点是正）**: TODO 内の「空/不在/無し/スタブ」と断定する記述（例: 「`UI/ChallengeList/` も空スタブ」）を、実コードで**反証確認**する（実際には複数ファイルが在る等の「在るのに無いと書いた」誤りを検出）。
6. **3D 連動 UI の二重計上防止**: (I)/(II) 境界 UI（HP バー等）は TODO §2.8（ワールドアンカー）と §2.5（モード）のどちらに**1 回だけ**計上したか突合表に列で記録。

**出力**: 突合表。各行に**必須列** `ui` / `evidencePath`（実ファイル絶対パス）/ `line`（行番号）/ `responsibilityEvidence`（責務根拠 1 行）/ `対応FEAT` / `判定`（一致 / 誤分類 / 未反映）/ `3D計上先`。＋ TODO 否定的主張の反証結果。これが §5 条件 5 の機械ゲートの入力（空欄＝未検証＝不合格）。
**完了条件**: 全確定 UI に判定と evidence 3 列が付き、誤分類・未反映・否定的主張の誤りの差分リストが揃う。

### Phase 5: 反映と収束判定
**目的**: 差分を TODO に反映し、網羅性が安定したことを確認する。

1. Phase 4 の「誤分類」「未反映」「否定的主張の誤り」を `cef-webui-migration-todo.md` へ反映（FEAT 追加・分類是正・§5 更新）。
2. TODO §5 のチェックリストに**軸 A（ディレクトリ/ファイル台帳起点）・軸 C（オーバーレイ起点）の行**を追加（従来の状態軸だけに依存させない）。
3. 収束判定（下記 §5 受け入れ条件）を満たすまで Phase 1〜4 を再走。**各再走は grep 語彙を変えるだけでなく、独立した検証軸を 1 つ追加する**（例: 別担当による再分類 / 継承グラフからの逆引き〔基底クラス→派生 UI〕/ prefab・uxml 参照起点からの逆引き）。grep は母集団でないため「語を 1 つ足した」だけでは独立検証にならない。軸を出し尽くしたら「検証軸 N 種で差分 0、追加軸なし」と明記して止める（終了は §5 末尾の 3 周上限が保証）。

**完了条件**: §5 の受け入れ条件をすべて満たす。

---

## 4. 成果物

- 本書（再監査の手順定義）。
- 分類表（Phase 2）/ 状態外 UI 一覧（Phase 3）/ 突合表（Phase 4）— 監査ログとして残す（`/tmp` か作業メモ）。
- 更新後の `cef-webui-migration-todo.md`（差分が出た場合）。
- 必要なら学習メモ `ui-completeness-off-state-overlays.md` の「確認 grep」を本書の Phase 1 コマンド群へ更新。

---

## 5. 受け入れ条件（収束判定）

以下を**全て**満たしたら網羅性が再保証されたとみなす。**自己参照（リスト内整合）でなく母集団件数との突合を軸にする**:

1. **件数突合（母集団網羅）**: 台帳B（全ソースルートの全 `.cs`）の**実行時件数 = triage 件数**。`find "${roots[@]}" -name "*.cs" | wc -l` の値と分類表の行数が一致（`.cs` 取りこぼし 0 を件数で証明）。
2. **全ソースルート被覆（台帳0 件数突合）**: `find . -mindepth 1 -maxdepth 1 -type d` と `find . -name "*.asmdef"` の**全行**が台帳0 で「対象/除外(根拠)/Editor」に分類済み（未分類行 0）。roots 配列＝「対象」分類と一致。「対象」の全ルートが台帳B に含まれ triage 済みで、`Client.Game/Common/GameStateController.cs`・`Client.Game/Skit/SkitManager.cs`・`Client.Localization/TextMeshProLocalize.cs` を名指し確認。除外行（Input/Network/Mod/WebUiHost/Tests/Editor）は「UI 非産出」を Read 確認済み。
3. **dir 台帳消化**: `find Client.Game -mindepth 1 -maxdepth 2 -type d | wc -l` の**実行時件数**と、完了印の付いた dir 数が一致（固定値を埋めない）。各 dir に「UI 有り/無し + 根拠」記入済み。
4. **資産台帳被覆**: 台帳C（**prefab/uxml/uss**）の各 UI 資産が、母集団内 `.cs`（Addressables/`[SerializeField]`/UIDocument 参照元）に紐づき Web 移行対象として突合表に記載済み。`.cs` ドライバから辿れない孤立 UI 資産が 0（Unity 依存グラフで双方向確認）。
5. **責務検証完了（双方向・機械ゲート）**: TODO §5 の全 `[x]` 項目に「検証した実ファイル絶対パス + 行番号 + 責務根拠 1 行」が突合表へ記載済み（**空欄＝未検証＝不合格**）。名前一致のみの項目が 0。かつ TODO 内の「空/不在/無し」断定が全件、実コードで反証確認済み。
6. **軸の置換が成果物に反映**: TODO §5 チェックリストに軸 A（dir/ファイル台帳起点）・軸 C（オーバーレイ起点）の行が追加済み（背骨が状態機械から置換されたことの確認）。
7. **独立軸を足した再走で差分 0**: 最後の再走（Phase 1〜4）で新規の「未反映 / 誤分類 / 否定的主張の誤り」が 0 件。**かつ当該再走で独立検証軸（継承逆引き/資産逆引き/別担当再分類）を 1 つ以上追加済み**（grep 語の追加だけは独立検証と認めない）。
8. **多視点レビュー収束**: 本計画と更新後 TODO に対する多視点レビューで新規 Critical = 0。

> 上限: 再走は 3 周まで。3 周で差分 0 にならなければ残件を列挙してユーザー報告（無限ループ防止）。

---

## 6. リスク・既知の落とし穴

- **grep を母集団にしない（最重要）**: grep（フラグ1/2）はノイズを拾い、かつ取りこぼす（カスタム基底 `ItemGetDebugSheet : DefaultDebugPageBase`、トークン無しの子 View `BackgroundSkitUI`、フラグ1 が捕まえない `BackgroundSkitManager`/`SkitManager`）。**母集団は常に台帳B（全 `.cs`）+ 台帳C（UI 資産）**。grep はふるいでなくフラグ。「名前が ...Processor だから UI でない」と決めつけず中身を Read（P3）。
- **3D 連動 UI の二重計上/取りこぼし**: `MapObjectHpBarView` 等は (I)/(II) の境界。Phase 4 step 6 の突合表で TODO §2.8（ワールドアンカー）/§2.5（モード）のどちらか一方に必ず 1 回計上。
- **ソースルート越境（兄弟の隙間の再発）**: 是正対象は「UI/ の兄弟ディレクトリ」だったが、同型の隙間が**アセンブリ/サブディレクトリ単位で多段に**再発しうる（当初 `Client.Localization`/`Common`/`Starter` が grep から漏れ、次に `Client.Game/InGame` 限定で兄弟 `Client.Game/Common`・`Client.Game/Skit` が漏れた）。§1 のソースルートは固定リスト、母集団 find は `Client.Game` 全体、§5 条件 2 で名指し全件照合する。
- **`.cs` 限定の隙間（資産種別軸）**: UI Toolkit の `.uxml`/`.uss` は `.cs` 母集団外（`Assets/Scripts` の外にすらある）。台帳C で別途列挙し、各 (I) UIDocument クラスに紐づける。**件数突合（条件 1）は `.cs` 内部の取りこぼし 0 を証明するが、母集団定義そのものの正しさ（UI を出す全資産種別を含むか）は証明しない**——種別カバレッジは件数突合の外で別途担保する。
- **MainMenu の別シーン性**: D3 でスコープ外（uGUI 残置）だが、**棚卸しからは外さない**（スコープ判断と網羅監査を混同しない。漏れていないことを確認した上でスコープ外と明記する）。
- **「状態外」の再帰**: 新たに見つかった状態外 UI が、さらに別の状態外 UI を起動する場合がある。Phase 3 step 4 で発火経路を辿り、1 パスで終えない。
- **収束の自己参照罠**: 「リストが埋まった＝網羅」は過去に再監査を騙した型。収束は §5 条件 1/3 の**母集団件数突合**と、条件 7 の**独立検証軸の追加**で外部基準に縛る。件数突合は母集団定義の正しさを保証しない点に留意（上記「資産種別軸」）。
