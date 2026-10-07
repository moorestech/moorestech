# .claude/skills/moores-code-review/tests/fake_uloop.py
# test_applied_diff_checks.py 用の uloop execute-dynamic-code の代役。<project-path>/../fake_api.json を
# 「Unity がコンパイル済みの公開 API」とみなし、スニペット内の `Type.Method(args)` 呼び出しを Roslyn 風の診断にする。
# 名前を含まない型不一致（CS1503）も出すので、名前 grep に頼らない前後比較の検証に使える。
# fake_api.json が無ければ何も出さず失敗する（Editor 不在の再現）。
# Stand-in for `uloop execute-dynamic-code`: treats fake_api.json as the compiled public API and reports
# Roslyn-like diagnostics (including the name-less CS1503) for `Type.Method(args)` calls. No file = no Editor.
import json
import re
import sys
from pathlib import Path

CALL_RE = re.compile(r"\b([A-Z]\w*)\.([A-Z]\w*)\(([^()]*)\)")


def arg_type(arg: str) -> str:
    arg = arg.strip()
    if re.fullmatch(r"-?\d+", arg):
        return "int"
    if arg.startswith('"'):
        return "string"
    return "object"


def diagnose(code: str, api: dict) -> list[dict]:
    errors = []
    for line_no, line in enumerate(code.splitlines(), 1):
        for type_name, method, args in CALL_RE.findall(line):
            key = f"{type_name}.{method}"
            if key not in api:
                errors.append({"ErrorCode": "CS0117", "Line": line_no, "Column": 5,
                               "Message": f"'{type_name}' does not contain a definition for '{method}'"})
                continue
            params = api[key]
            given = [arg_type(a) for a in args.split(",") if a.strip()]
            for i, (want, got) in enumerate(zip(params, given), 1):
                if got != want:
                    errors.append({"ErrorCode": "CS1503", "Line": line_no, "Column": 9,
                                   "Message": f"Argument {i}: cannot convert from '{got}' to '{want}'"})
    return errors


def main(argv: list[str]) -> int:
    project = Path(argv[argv.index("--project-path") + 1])
    snippet = Path(argv[argv.index("--code-file") + 1])
    api_path = project.parent / "fake_api.json"
    if not api_path.is_file():
        print("connection refused", file=sys.stderr)
        return 1
    errors = diagnose(snippet.read_text(encoding="utf-8"), json.loads(api_path.read_text(encoding="utf-8")))
    print("[uloop] executing...")
    print(json.dumps({"Success": not errors, "Result": "" if errors else "compile-only",
                      "CompilationErrors": errors, "ErrorMessage": "compile failed" if errors else ""}))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
