# 復旧記録: PR #993 先行マージの巻き戻し（2026-07-07 20:40台）

## 経緯（1行）

#993（playtest-stabilization）を先行マージしたが、差分に設置システム刷新の土台104コミットが構造的に含まれることが判明し、ユーザー判断で**実装順マージ（#987が先）へ方針変更**。GitHub上のrevert（#994）に加え、revert毒を残さないため master-fable-tmp を force-push で #993 マージ前へ巻き戻した。

## 関連コミットハッシュ全記録（moorestech リポジトリ）

| 対象 | ハッシュ | 内容 |
|---|---|---|
| **master-fable-tmp 現在値（巻き戻し後）** | `762af74b5` | Merge PR #992（gear-connect-add-shape） |
| force-push前のtip | `54a007f53` | Merge PR #994（#993のrevert） |
| #994のrevert実体 | `8c4736a06` | Revert "feat: プレイテストDSL基盤…" |
| #993のマージコミット | `94c4c72f8` | Merge PR #993 |
| feature/playtest-stabilization tip（local/remote一致） | `bd3801db7` | docs: マージ完了メモ（※メモ内容は巻き戻しで失効） |
| 同ブランチ主要コミット | `4600b7972` | シナリオの#992ジェネリクス追従fix |
| 〃 | `29f3be5d9` | 申し送りドキュメント追加 |
| 〃 | `d6e508228` | master-fable-tmp（#992分）2巡目マージ |
| 〃 | `ebb4788be` | masterピンを3b2de6fへ更新 |
| 〃 | `7098ff9a8` | master-fable-tmp（#989分）1巡目マージ |
| 設置システム刷新の土台tip（#987のbase側祖先） | `1877b7315` | belt-conveyor-place-systemマージ |
| feature/replace-place-system tip（#987 head） | `343f6108e` | Sync legacy HUD（129コミット差＝全量に復元済み） |

## 関連ハッシュ（moorestech_master リポジトリ）

| 対象 | ハッシュ | 内容 |
|---|---|---|
| playtest-stabilization-integration tip（push済み） | `3b2de6f` | blocks.jsonドメイン別コネクタ移行（584a14eベース） |
| 統合ベース | `584a14e` | 本ブランチ互換のmaster系列コミット |
| master-fable-tmpの既存ピン | `9c0993f` | ※#989スキーマとデータ不整合の疑いあり（申し送りハマりどころ7） |
| hermes/mt-014のデータ移行 | `645b0fa` | #989対応の元変換（別系列） |

## ローカルバックアップタグ（playtest worktreeに作成済み）

- `backup/pre-forcepush-master-fable-tmp` = 54a007f53
- `backup/pr993-merge-commit` = 94c4c72f8
- `backup/playtest-stabilization-tip` = bd3801db7

GitHub側でも #993/#994 のマージコミットはPR参照で保持されるため消失しない。

## 復旧コマンド（万一戻したくなった場合）

```bash
# 巻き戻し自体を取り消し、#993マージ+#994revert状態へ戻す
git push --force-with-lease=refs/heads/master-fable-tmp:762af74b5 origin 54a007f53:refs/heads/master-fable-tmp
# #993マージ直後（revert前）の状態へ戻す
git push --force-with-lease=refs/heads/master-fable-tmp:762af74b5 origin 94c4c72f8:refs/heads/master-fable-tmp
```

## 巻き戻し後の検証結果（2026-07-07 20:41）

- origin/master-fable-tmp = 762af74b5 ✅（#993マージ前と完全一致）
- #987 実効コミット数 = **129（全量復元）** ✅ revert毒なし
- feature/playtest-stabilization = bd3801db7 のまま無傷（プレイテスト成果は全て保持）✅
- moorestech_master は今回の操作で一切変更なし ✅

## 新方針（ユーザー決定・2026-07-07 20:40）

**実装順でマージする。つまり #987（設置システム刷新）を先に仕上げてマージし、その後 #993 を再作成（新PR）してマージする。**
旧申し送り（2026-07-07-pr993-merge-handoff.md）の「#993先行マージ」方針は**誤った記録であり撤回**。同ドキュメント冒頭に撤回注記あり。

再マージ時の注意: #993はマージ済みPRのため再オープン不可。feature/playtest-stabilization から新規PRを作る。その際 #987 マージ後の master-fable-tmp を取り込んでから出すこと。
