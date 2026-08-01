# クラフトタブ右辺平滑化・接触修正 レビュー記録 (2026-08-01)

前回記録: [2026-08-01-craft-tab-corner-parity.md](2026-08-01-craft-tab-corner-parity.md)

## 対象
- base: `7de71923eb34aa607fd61bcdd2ea87e0c6af2251` / reviewed head: 同HEAD上のstaged差分（7ファイル、152 additions / 11 deletions）
- ブランチ: `sakastudio/web-ui-craft` / PR: #1114
- context要約 — ゴール: 右辺の意図的2px段差を滑らかな直線へ置換し、タブ下端をパネルへ接触させ、ハンマーを不変に保つ / 非目標: クラフトタブ外の全画面差と補助比較器のCI化 / 許容トレードオフ: 平滑化に伴う正本の段差形状からの意図的乖離、右側面だけ10%面積許容 / 制約: 既存5層SVG、既存配色、パネル地色を変更しない

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | なし | confirmed 0、比較演算子候補0。最終ファイルは111/165行以内 |
| terra high reviewer群 | 2件を適用 | 2px階段の偽陽性と透明・クリップ時の接触偽陽性を負例つきで解消。最終whole-diffはCritical/Important/Minorなし |
| Codex外部監査 | High 2件を適用 | paint祖先をcraft panelまで拡張し、接触をpanel topと直上行の連続runで判定。再監査で両件ADDRESSED、新規Critical/Highなし |
| Fable全般相当 | なし | legacy Fableの代わりにterra highのホリスティックレビューを実施 |
| post-checks | 5件を適用 | 長い説明コメントを機械的短縮。根拠保全Criticalなし、最終コメント規約Criticalなし |

## 適用した修正
- 右辺を単一ポリゴンへ置換し、背面だけ2 SVG px延長。ハンマーpath不変（ユーザー指摘）→ 本記録と同一コミット
- 全60行・端点delta 0..1、paint祖先、fill、bbox接触をPlaywrightで固定（terra high reviewer）→ 本記録と同一コミット
- panel topと直上行の接続画素・最長runを比較器へ追加（Codex外部監査）→ 本記録と同一コミット
- コメント5組を規約内へ短縮（comment-convention-guard）→ 本記録と同一コミット

## 設計判断（AskUserQuestion裁定）
- 新規裁定なし。ユーザーが明示した「ジャギーは不要」「下の部分と触れ合わせる」を優先した。

## 破棄した指摘
- 比較器を通常E2E/CIへ接続する指摘 — 正本3270×1844画像をリポジトリ外入力とする補助QAで、この追修正の非目標。Playwrightに恒久契約を実装済み。
- PR掲載用`full-current.png`を比較器の3270×1844入力にする指摘 — 掲載画像と機械比較入力は用途が異なり、比較は再現コマンドと原寸一時キャプチャで22/22確認した。
- 1pxごとの離散化も矩形ジャギーとみなす指摘 — ベクタ斜辺のラスタライズにも0/1px進行は必須であり、今回の旧2px段差はdelta上限と正確なpath契約で排除する。

## 事後結果（マージ後追記可）
- なし

## メタ
- セッションID: Codex外部監査 `019fbd16-331d-7681-beac-e9d964aed38c` / スキップ系統: legacy Opus・Sonnet・Fableは利用不可のためterra highで代替 / 備考: 新画像22/22、旧画像20/22、prod/dev全E2E各123件を最終diffで確認
