# Task 12: 目視QA + 実マスタ検証 レポート

対象ブランチ: `feature/build-menu-categories`
worktree: `/Users/katsumi/moorestech-worktrees/build-menu-categories`
実施日: 2026-07-22

---

## サマリ

- 建設メニューのカテゴリ化UIは mock-host 目視QA・Unityコンパイル・各テストで問題なし。
- **Step 3（実マスタPlayMode確認）はスキップ**。理由: 固定ポート11564を別セッション（メインリポジトリ `/Users/katsumi/moorestech` の Unity PID 74492）が PlayMode 実行中で占有しており、殺さない方針のため起動を試みず。
- 重大な挙動バグ・様式違反は未検出。修正コミットは QAハーネス+スクショ+本レポートの追加のみ。

---

## Step 1: mock-host 目視QA

`e2e/capture-eval.ts` を前例に、BuildMenu 専用の撮影ハーネス `moorestech_web/webui/e2e/capture-buildmenu.ts` を新規作成し、DEMO mock-host を起動して `/__uistate?state=BuildMenu` で建設メニューを表示、3状態を撮影した（viewport 1284x725 ×dSF2 = 2568x1450）。

スクショ（`.superpowers/sdd/qa-screenshots/`）:
1. `buildmenu-1-default.png` — 既定表示（先頭カテゴリ「物流」選択）
2. `buildmenu-2-search.png` — 検索中（"鉄" 横断検索・複合見出し・サイドバー無効）
3. `buildmenu-3-hover.png` — ホバープレビュー（木のチェスト＋必要素材コスト表示）

### §10チェック項目の所見

- **4辺の余白**: パネル(759x525固定)の4辺とも内容がフェード帯に載っておらず、切れてもいない。右端は `--build-menu-edge-safe-area(16px)` の padding-right でスクロールバーとパネル右フェードの間に余白が確保され良好。下端は `--block-panel-bottom-safe-area(58px)` でホットバー手前に収まる。左（サイドバー）・上（タイトル/検索）とも十分な余白。
- **フェード帯との干渉**: なし。GamePanel 共通の下端3枚三角装飾（`.bottomDeco`）はモックのエントリ数が少ないため空き領域に大きく露出して見えるが、これは全パネル共通の意図的装飾（z-index0でグリッド背後に敷かれ、実マスタでエントリが増えれば自然に隠れる）であり本機能固有の欠陥ではない。
- **見出しの区別**: 各サブカテゴリ群に見出し（「チェスト」「電気コンベア」）＋FadeRule が付き、無札スロット群の並置は無し（§8.11準拠）。検索時は「物流 / チェスト」形式の複合見出しで出自を明示。
- **中央揃え/対称**: サイドバーのカテゴリラベル（物流/輸送/ブループリント）は縦ModeSwitchのボタン内で中央揃え、長ラベル「ブループリント」も見切れなし。
- **選択状態の識別**: 選択中カテゴリ（物流）は明トーン、非選択は暗トーンで区別。検索中はサイドバー全体が dim（data-disabled=true）。

> DEMO配信ではフィクスチャのアイコンパス（例 `/icons/wood-chest.png`）が数値ID解決に載らず placeholder/broken-image になるため、スロット内に alt テキスト（ラベル）が折返し表示される。これは撮影環境固有のアーティファクトで、実ゲームの実PNGでは正常表示される。様式・レイアウトの判定には影響しない。

**結論: 様式違反・レイアウト不良なし。**

---

## Step 2: クライアント全テスト + 補助テスト

| 系統 | コマンド filter | 結果 |
|---|---|---|
| Unityコンパイル | `uloop compile` | Success / 0 errors / 0 warnings |
| Unity BuildMenu/契約 | `WireContractTest\|BuildMenu` | 14/14 PASS |
| Unity 広域回帰 | `Tests\.(UnitTest\|CombinedTest)` | 919/919 PASS |
| webui unit (vitest) | buildMenu/contract/uiState | 82/82 PASS |
| webui e2e (playwright) | `buildMenu.spec.ts` | 8/8 PASS |

- MooresmasterLoaderException は未発生（コンパイル・BuildMenu/契約テストとも正常初期化）。master pin は commit 45994d669 で 102cd14（blockCategories込み）へ更新済み。

---

## Step 3: 実マスタ PlayMode 確認 — スキップ

- **スキップ理由**: 固定サーバーポート11564を別セッションが占有中。
  - `lsof -iTCP:11564`: Unity PID 74492（`/Users/katsumi/moorestech` メインリポジトリ）が LISTEN + ESTABLISHED 接続を保持＝PlayMode実行中。cef-unity(84070)も同ソケット共有。
  - ブリーフの指示「起動失敗したら殺さずその旨を報告して観点3をスキップ」に従い、PlayModeは起動せず。
- なお本worktree（build-menu-categories）には `cache/` ディレクトリが存在せず、DebugParameters の FreeBlockPlacement 残留（全ブロック表示化）の罠は本worktreeでは無効（起動すれば既定値false）。
- カテゴリ順・坂バリアント非表示・車両の輸送カテゴリ合流・検索/プレビューは、純関数ロジック（`buildMenuGrouping.ts`：カテゴリ配列＝サーバー配信のblockCategories定義順を保持しフィルタのみ）とUnit/e2eテストで担保されているが、実マスタ配信データそのものでの目視確認は未実施のため、ポート解放後の別途確認を推奨。

---

## Step 4: 修正・コミット

- 挙動バグ/様式違反レベルの修正は不要（未検出）。
- 追加物: QA撮影ハーネス `e2e/capture-buildmenu.ts`、スクショ3枚、本レポート。
- 共有ワーキングツリーの他セッション変更（`.superpowers/sdd/task-4-report.md`）はコミットに含めない。
