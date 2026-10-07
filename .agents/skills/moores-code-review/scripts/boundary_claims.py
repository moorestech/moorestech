# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""try-catch の根拠コメントが主張する外部境界の判定語（checks_static.py が参照）。
Boundary-claim words for try-catch rationale comments (used by checks_static.py)."""

# AGENTS.md が try-catch を許すのは外部境界5種だけ。根拠コメントの「主張内容」をこの表と照合する
# AGENTS.md permits try-catch only at these 5 external boundaries; the rationale comment's claim is matched here
BOUNDARY_ALLOWLIST = {
    "external-process": ("外部プロセス", "プロセス起動", "外部コマンド", "サブプロセス",
                         "Process.Start", "ProcessStartInfo", "subprocess", "external process"),
    "network-io": ("ネットワーク", "通信", "WebSocket", "websocket", "ソケット", "Socket",
                   "socket", "HTTP", "http", "TCP", "network"),
    "external-json-parse": ("パース", "parse", "Parse", "Deserialize", "デシリアライズ",
                            "JsonConvert", "JsonSerializer", "外部入力", "外部JSON"),
    # ディスクIOは2026-09-14にAGENTS.mdへ明記された境界（他プロセスのロック・権限・容量不足）
    # Disk IO became an explicit boundary in AGENTS.md on 2026-09-14 (foreign locks, permissions, full volume)
    # 「権限」「ロック」「File.」等の単独語は境界でないtry-catchも通してしまうため、ディスクを名指しする語と複合語だけを許す
    # Bare words like "権限", "ロック" or "File." would wave through try-catch that is no boundary at all, so only words naming the disk and compounds are allowed
    "disk-io": ("ディスクIO", "ディスク", "disk", "Disk", "ファイルシステム", "file system",
                "容量不足", "空き容量", "アクセス権", "書き込み権限", "読み取り権限",
                "ファイルのロック", "他プロセスのロック", "file lock",
                "IOException", "UnauthorizedAccessException"),
    # 外から送られ動的にコンパイル・実行したコードの実行境界（2026-09-28 AGENTS.md 第5類型）
    # Execution boundary of code sent from outside and compiled/run dynamically (AGENTS.md 5th kind, 2026-09-28)
    "dynamic-code": ("動的に実行", "動的にコンパイル", "動的コンパイル", "動的コード",
                     "送られたコード", "送信されたコード", "dynamically compiled",
                     "dynamically executed", "submitted code", "submitted external code"),
}
