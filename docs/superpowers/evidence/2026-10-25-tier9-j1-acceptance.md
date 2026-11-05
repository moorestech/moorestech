# Tier9 J1受入れ（W1→W2）

## 判定

**J1 PASS（controller、2026-10-25）。** ユーザー裁定を反映したTasks9・10の独立再レビューがSpec✅／品質Approvedとなり、A/B両方のW1条件を満たした。BのTasks25〜26を解放する。最終出荷マスタ・最終ピンの合格やTier9統合全体の完了を意味しない。

- 裁定: `.decisions/2026-10-25-Tier9のW1受入れを既存CI範囲とB統合マスタで検証し最終ピンの受入れは維持する.md`
- 計画: `docs/superpowers/plans/2026-10-25-tier9-integration.md` A33・A34、オーケストレーション§3。
- 裁定・既存の通しプレイ方針をcommit/pushした本体HEAD: `7beff877ff2d592d5b392fe950e726a1631027e9`。検証前後のHEAD・status・remote一致を保存。以後この受入れ記録とチェックボックスの変更はdocsのみ。
- W1マスタ: `1cf0df8fbae21d1c34395c5afafceeee2a5b3113`。前後cleanかつremote一致。共有cloneと本体ピン `630fdce08eccee47feaa15721ca607f407000ab9` は未変更。

## Aの条件

| 条件 | 観測・証拠 |
|---|---|
| Tasks4〜8の実装・レビュー・push | 各独立レビューApproved。セーブ移行はCurrentVersion8、V6→V7を保持しV7→V8へ部品を統合。進捗台帳のcomplete行と各Taskコミットに対応 |
| compile | 生JSONでError0／Warning0、正常開始のErrorログ0。差分compileの警告0を過去の全警告解消とは解釈しない |
| P1主要regex | save401/401、Tier9 607/607、research/unlock/WebUI255/255 PASS。後ろ2本はPlayMode遷移の切断後に、自分の実行開始後かつ全ケースが要求filterに一致するXMLを回収 |
| Task9自動回帰 | 既存CIと同じIgnoreCI除外範囲3298/3298 PASS。原XMLの非IgnoreCIと新runのfullname多重集合は欠け0／余り0。Tier9起因の除外追加0 |
| Web・外部依存 | tsc2本exit0、vitest165files／1354tests PASS、参考eslintと外部依存lintもexit0。Node26.12.1とpnpm9.15.0を記録 |
| Task10入力 | 明示Bコミットからarchiveしたserver_v8の全253filesがgit blobと一致。正常入力のmanifestは前後不変 |
| G1正例・負例・復旧 | B archiveでLOAD PASS research44。複製の石窯allowQualityModuleだけ削除した負例はFAIL＋Error、B archiveへRESTORED。復旧後の観測値・ハッシュは正例と一致。全オブジェクト同一性は主張しない |
| G1 batch | 同じB archiveの正例0／破損負例1／引数なし1。Unityプロセス自体の終了コードを保存 |
| Task11・G3 | 独立最終レビューApproved。全研究GUIDの一致・重複なしを実行前に検証。同じB archiveで44研究PASS、原始研究2を先頭へ移した順はFAIL。G3 batchも正常順0／順序破壊1 |
| Tasks9・10再レビュー | `task-9-ruling-review.md` と `task-10-ruling-review.md`、それぞれSpec✅／品質Approved、Critical／Importantなし |

## Bの条件

- Tasks1〜3をマスタ統合ブランチへpush済み。通常mergeは `ee1153a`＋`a879158`、G2は `4ec6a5e`＋修正 `9447df7`、G6は `1cf0df8`。各タスクは別担当の独立レビューApproved。
- G2の基準入力は44/44研究・21waves、既知の欠落69failures／19allowedでexit1。青銅の出力レシピ3件削除の負例は2/44研究・301failures／19allowed、exit1。原ログとexitファイルをcontrollerが確認した。
- G2自己テスト48件、G6自己テスト14件成功。G6の未実装区間等による7件の赤は後続データ投入前の状態で、緑とは主張しない。
- マスタ書込みはBからAへ解放済み。BはJ1後にtests worktreeへ `origin/feat/tier9-integrated` を**通常マージ**し、Tasks25→26を実装する。マスタ・CurrentVersion等の本番コードはA所有。
- Bの入力・契約差・MinorはBの `docs/superpowers/evidence/2026-10-25-tier9-session-b-handoff.md` と記録repoのsession-bへ保存済み。旧masterの65件条件は必須キー不足で拒否されるため、最終コードで達成したとは扱わない。名前ペアAPIは既存契約維持、G3のGUID order契約は維持。

## 明記して残す未達・所見

1. **原全assemblyは3319件中3298 PASS／21 FAIL。** 21件は既存IgnoreCIの手動Surface検査。自動回帰の成功に置き換えず、手動検査の未達としてPR本文へ引き継ぐ。従来から失敗していたかはBASE同条件の証拠がないため断定しない。
2. **現ピン630fdceはorbitalPayloads欠損でG1 FAIL。** 最終ピンでのR7/G1・R9/G3・正負例・復旧・batch・実CI合格はTask24後の必須条件として残す。W1のB archive成功で代替しない。Linux CIの認証・ライセンス・cache・mountは未確認。
3. テスト後Error153件には負例ログを含むが、XML全文へ対応付くのは146件。切断2件・IO等5件の計7件の帰属は未確定。すべて期待ログとも新規バグとも断定しない。正常開始Error0・3298PASSとは別の所見。
4. vitestのMissing translation keyを含む441行、成功batch終了時のCurl42／23を保持。数だけで製品バグ・ゲート起因と認定しない。
5. CI対象テストの子コマンド1本は600秒制限でbackground実行された運用逸脱がある。親Claudeは前景で完了まで待ち、exit0・JSON・XML終端・件数・時刻が整合するため独立レビューは試験結果を採用。今後は前景で待てる経路を選ぶ。
6. Task30/31の説明に残った配布ビルド・promotion.md要求は削除。ユーザーはMacBook Proの既存world_1を直接開く。配布ビルド・staging・セーブ書戻しは行わない。

## 証拠の保存先

記録repo `moorestech_logs/harness/sdd/feat-tier9-integrated/workspace/`:
- `w1-ruling-revalidation-report.md`、`w1-ruling-revalidation-evidence/`（原JSON/XML・archive・manifest・コマンド・batchログ・終了コード・前後state）
- `task-9-ruling-review.md`、`task-10-ruling-review.md`、`task-11-final-review.md`
- 元の全assembly結果 `task-9-evidence/step5.xml` 等は不変のまま保持。

B原証拠: `moorestech_logs/harness/tier9-integration/session-b/task-{1,2,3}/`。Codex枠は2026-10-25T12:46:31.876Z時点で週次10%使用、残90%でW2目安30〜40%を賄える。切替え条件に該当せず。
