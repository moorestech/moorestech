"""接続ファイルの外部入力検証。 / Validate the external access file."""
import json
import os
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

# 死んだプロセスの残骸を生きた入口として使わない。撤去に失敗した access.json はここで拒否する
# Never treat a dead process's leftovers as a live entry; an access.json that failed to be withdrawn is refused here
process_id = access.get("processId")
if type(process_id) is not int or process_id <= 0:
    sys.exit("ERROR: access.json processId is invalid")
# プロセス生存確認はOS境界。存在しない・確認できないなら拒否する
# Probing liveness is an OS boundary; a missing or unverifiable process is refused
try:
    os.kill(process_id, 0)
except ProcessLookupError:
    sys.exit(f"ERROR: access.json のプロセスが生きていない（撤去漏れの残骸）: pid {process_id}")
except PermissionError:
    pass
except OSError as error:
    sys.exit(f"ERROR: access.json のプロセス生存を確認できない: pid {process_id}: {error}")
print(port)
print(token)
