"""接続ファイルの外部入力検証。 / Validate the external access file."""
import json
import sys
from pathlib import Path

# ディスク・外部JSON境界の失敗を拒否理由として返す
# Report disk and external JSON boundary failures as rejection reasons
try:
    access = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
except (OSError, ValueError) as error:
    sys.exit(f"ERROR: access.json を読めない: {error}")
if not isinstance(access, dict):
    sys.exit("ERROR: access.json must be an object")
port, token = access.get("port"), access.get("token")
if type(port) is not int or not 1 <= port <= 65535:
    sys.exit("ERROR: access.json port is invalid")
if not isinstance(token, str) or not token or any(ord(c) < 33 or ord(c) > 126 for c in token):
    sys.exit("ERROR: access.json token is invalid")
print(port)
print(token)
