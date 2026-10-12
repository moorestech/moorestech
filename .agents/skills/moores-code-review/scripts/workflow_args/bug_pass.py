# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""workflow_args/bug_pass.py — 最終バグ確認パス（SKILL.md Step 7.5）の系統選択の正本。

bug-pass は裁定反映まで済んだ最終 head の PR 全体 diff を、誤動作（正しさ）だけに絞って見直す。
系統の許可集合はこのファイルの定数だけが持つ（SKILL.md・手順書は名前を列挙しない）。発火条件は
通常どおり select_reviewers.py が判定し、その結果を許可集合で絞る（.ts だけの PR に cs 観点を出さない）。
根拠: harness/pr-independent-review/experiments/2026-10-06-value-audit/report.md・
harness/moores-code-review/experiments/2026-10-05-review-until-no-critical/report.md（moorestech_logs）。

Source of truth for the bug-pass system selection. Firing is decided by select_reviewers.py as usual
and then narrowed to the allow-lists below; context exemptions are stripped from the user prompt.
"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

from workflow_args.systems import SCRIPTS

# 誤動作を直接狩る reviewer だけ。構造・規約・命名・重複・抽象化・前例一致・効率・テスト品質・依頼解釈は外す
# Only reviewers that hunt wrong behavior; structure/convention/naming/duplication/abstraction/precedent/cost/test-quality/intent are out
BUG_PASS_REVIEWERS = {
    "core-any-callsite-tracer": "変更関数の呼び出し元を全数追い、新しい事前条件・戻り値変化で呼び出し側が壊れないか",
    "core-any-removed-invariant": "削除・置換した行が守っていたガード・不変条件の消失（退行）",
    "core-cs-async-cancellation": "await 中に持ち主が破棄されても止まらない非同期処理（破棄後アクセス・二重実行）",
    "core-cs-result-state-propagation": "失敗分岐が成功・空ペイロードに潰れて呼び出し側が誤った結果で進む経路",
    "core-ts_tsx-result-state-propagation": "同上の webui 版（fetch 結果→表示分岐で失敗が消える経路）",
    "core-ts_tsx-react-antipattern": "フック・状態更新のバグ直結パターン（古い状態・無限再描画・戻れない画面）",
    "moores-any-server-state-sync": "サーバー状態がクライアントへ届かない・古いまま残る同期経路の欠落",
}
# 全文精読の正しさ系2観点。チャンク内一貫性（重複・非対称）は構造系なので外す
# The two correctness investigators; chunk-context-consistency (duplication/asymmetry) is structural and excluded
BUG_PASS_INVESTIGATORS = {
    "chunk-deep-correctness": "変更後ファイル全文を読み、不変条件・状態遷移と変更の不整合を狩る",
    "chunk-seam-integration": "呼び出し元・実装先・プロトコル対向・登録箇所との縫い目の整合",
}
# Codex はバグ狩り専任の1本だけ（俯瞰・設計整合は外す）/ Codex runs the bug-hunt template only
BUG_PASS_CODEX_KINDS = ("bughunt",)
# split_chunks の閾値。小規模 PR でも全文精読を当てるため1ファイルから分割する
# split_chunks threshold: chunk from a single file so small PRs also get a full-file read
BUG_PASS_CHUNK_THRESHOLD = 1
# 免責として働く context の節。bug-pass では外す（ゴール・制約は残す）
# Context sections that act as exemptions; stripped in bug-pass (goals and constraints stay)
EXEMPTION_HEADINGS = ("許容するトレードオフ", "目指さない")

CONTRACT_ADDENDUM = (
    "\n重要な前提（bug-pass・最終バグ確認）: 裁定反映まで済んだ最終 head の PR 全体 diff を、誤動作だけに絞って見直すパス。"
    "この節は観点本文と上の契約より優先する。\n"
    "- Critical は再現手順（ゲーム上・開発上の誤動作）を1文で書けるものだけ。各件に `再現: <どの入力・状態で何が起きるか>` を必ず付ける。"
    "書けないものは Warning に落とす。\n"
    "- 構造・規約・コメント・命名・重複・抽象化・前例一致・効率・テストの書き方は報告しない。\n"
    "- 設計判断は常に「なし」。修正方針は報告された症状を消す最小の変更だけを書き、新しい型・interface・汎用化を提案しない。\n"
    "- 上の「依頼動詞優先ガード」はこのパスでは適用しない。User prompt から免責の節（許容するトレードオフ・非目標）は外してある。"
    "免責に当たりそうでも実害の再現手順が書けるなら Critical として返し、suppressed 節は使わない。\n")


def select_reviewers(patch: Path) -> list[dict] | None:
    # 発火判定と model は select_reviewers.py（model_map.json）に任せ、許可集合で絞るだけ
    # Firing and models come from select_reviewers.py (model_map.json); we only narrow by the allow-list
    run = subprocess.run([sys.executable, str(SCRIPTS / "select_reviewers.py"), str(patch)],
                         capture_output=True, text=True, timeout=120)
    if run.returncode != 0:
        print(f"select_reviewers.py が失敗: {run.stderr.strip()[:300]}", file=sys.stderr)
        return None
    rows = []
    for line in run.stdout.splitlines():
        if "\t" not in line:
            continue
        path, model = (part.strip() for part in line.split("\t", 1))
        if Path(path).stem in BUG_PASS_REVIEWERS:
            rows.append({"path": path, "model": model})
    return rows


def write_chunks(patch: Path, run_dir: Path) -> Path | None:
    # 非テストのレビュー対象ファイルが0本なら空 → investigator は不発火
    # Zero reviewable non-test files yields nothing, so no investigator fires
    run = subprocess.run([sys.executable, str(SCRIPTS / "split_chunks.py"), str(patch),
                          f"--threshold={BUG_PASS_CHUNK_THRESHOLD}"],
                         capture_output=True, text=True, timeout=120)
    if run.returncode != 0:
        raise SystemExit(f"split_chunks.py が失敗: {run.stderr.strip()[:300]}")
    target = run_dir / "chunks.tsv"
    target.write_text(run.stdout, encoding="utf-8")
    return target if run.stdout.strip() else None


def strip_exemptions(context: Path, run_dir: Path) -> Path:
    # `##` 見出し単位で免責の節を落とす（Step 1 が4カテゴリを `##` 見出しで書く規約に依存）
    # Drop exemption sections by `##` heading (relies on Step 1 writing the four categories as `##` headings)
    kept, skipping = [], False
    for line in context.read_text(encoding="utf-8").splitlines():
        if re.match(r"^##\s", line):
            skipping = any(h in line for h in EXEMPTION_HEADINGS)
        if not skipping:
            kept.append(line)
    kept.append("\n（bug-pass: 免責の節「許容するトレードオフ」「目指さない」は外してある）")
    target = run_dir / "context-bug-pass.md"
    target.write_text("\n".join(kept) + "\n", encoding="utf-8")
    return target


def carried_warning_sources(main_run_dir: Path) -> list[str] | None:
    # 本レビューの integrated.md（Warning 節）と、Workflow Refix・Step 7 裁定反映の再レビュー報告（agents/refix-*.md）
    # The main integrated.md (Warning section) plus the Workflow Refix and Step 7 re-review reports
    integrated = main_run_dir / "integrated.md"
    if not integrated.is_file():
        print(f"--carry-from に integrated.md が無い: {integrated}", file=sys.stderr)
        return None
    refix_reports = sorted((main_run_dir / "agents").glob("refix-*.md"))
    return [str(integrated.resolve())] + [str(p.resolve()) for p in refix_reports]


def expected_systems(reviewers: list, investigator_count: int) -> dict:
    return {"reviewers": len(reviewers), "verifiers": 0, "fable": 0,
            "investigators": investigator_count, "total": len(reviewers) + investigator_count}
