# eval — ハーネス有効性の測定

このディレクトリは moores-code-review（および上流のspec-architecture-review拡張）の有効性を測る装置。
測定は3層: **リプレイ評価**（変更のたび）・**前向きログ**（PRごと）・**ノイズ測定**（却下率）。

## Layer 1: リプレイ評価

レンズ・selector・deterministic_checks を変更したら必ず1回流す。

```bash
# 1. fixtureを再生成（コミットはマージ済みなのでSHAから永続的に再現できる）
.claude/skills/moores-code-review/eval/make-fixture.sh all

# 2. 決定論チェック（0トークン・まずこれだけでも回す）
for f in /tmp/moores-review-fixtures/*.diff; do
  echo "=== $f ==="
  python3 .claude/skills/moores-code-review/scripts/deterministic_checks.py "$f" --repo-root "$(pwd)"
done

# 3. selectorの発火確認（期待レンズが発火するか）
for f in /tmp/moores-review-fixtures/*.diff; do
  echo "=== $f ==="
  python3 .claude/skills/moores-code-review/scripts/select_lenses.py "$f"
done
```

フルリプレイ（レンズをサブエージェントとして各fixtureに当てる）はトークンを消費するため、
レンズ本文を大きく変えた時だけ実行し、`expected-findings.md` と突合して検出漏れ（recall）を確認する。
**注意**: fixtureは過去の状態のdiffだが、レンズはcwd（現在のコード）も読む。前例参照が現在形なのは
許容（前例は当時から存在した — cursor調査で確認済み）。

### 過学習チェック（ブラインドリプレイ）
22指摘はレンズの出典そのものなので、汎化確認には**レンズ作成に使っていない**マージ済みPR
（人間レビュー指摘が付いたもの）を1〜2本選び、同じ手順でリプレイして「人間指摘のうち何件を
ハーネスが先取りできたか」を見る。結果は log.md に記録する。

### synthetic/（ブラインド合成fixture）
レンズ本文に由来PRの実名が書かれている場合、由来PRへの再発火は名前照合で当たっただけの
可能性がある。`synthetic/` には**レンズと語彙が重ならない別ドメインの合成diff**（陽性=検出すべき・
陰性=偽陽性を出してはならない、各`-context.md`とペア）を置き、レンズ本文を変えたら3行契約で
両方に当てて「陽性=Critical あり／陰性=Critical なし」を確認する。
現行: `set-once-setter-positive.diff`（チャレンジ報酬通知のset-once setter→ありが正）/
`set-once-setter-negative.diff`（可変値SetHoge＋MonoBehaviour→なしが正）。2026-07-18 opusで両方合格。

### spec段階のリプレイ
PR988の誤設計は `docs/superpowers/specs/2026-07-05-item-stack-upgrade-design.md`（「新規プロトコル・
イベント・ハンドシェイク拡張は作らない」と明記）に現存する。spec-architecture-review を変更したら、
このspecを入力に「サーバー状態同期の3点セット逸脱が新規パターン/裁定事項として検出されるか」を確認する。

## Layer 2: 前向きログ（本命KPI）

マージ済みPRごとに `log.md` へ1行記録する（手動運用）。
主要KPIは **「設計クラス（F0/F1）の人間指摘数/PR」の推移**。5PRごとに定性振り返りを行う。

### 指摘の反映手順（手動）

1. `gh api repos/moorestech/moorestech/pulls/<PR>/reviews` と `/comments` でsakastudioの指摘を回収（LGTM・肯定は除く）
2. 故障モード分類: **F0**=specに誤方針が明記 / **F1**=役割同型の前例が存在した（rgで実在確認） / **F2**=既存ルールが既に明文化されていた
3. レイヤー別反映（1指摘1対策。「一般化ルール＋実例＋前例パス」の3点で書く）:
   - F0 → writing-plans の spec-architecture-review（Red Flags・実例）と layer-map
   - F1 → 該当レンズへ実例追記（無ければ新レンズ＋selector発火条件＋リプレイ確認）、layer-map「よく引っかかる箇所」
   - F2 → ルール文言を禁止調に強化。機械判定可能なら deterministic_checks.py へ
4. `log.md` に1行記録。将来のリプレイ対象なら `expected-findings.md` と `fixtures.tsv` にも追加
5. レンズ・スクリプトを変えたら Layer 1 のリプレイを回す

## Layer 3: ノイズ測定

レンズの指摘をユーザーが却下した件数も log.md に記録する。却下率の高いレンズはハーネス負債 —
検出率と同じ重みで監視し、過検知ガードの強化 or レンズ廃止を判断する。

## 故障モード分類（log.mdで使う）

- **F0**: 設計段階で誤りが確定（specに誤方針が明記）→ 対策先: spec-architecture-review / layer-map
- **F1**: 既存前例を探さず局所発明 → 対策先: precedent-alignment / 各レンズの前例追記
- **F2**: 明文化済みルールへの違反（最小差分バイアス）→ 対策先: ルール文言強化 + deterministic check化
