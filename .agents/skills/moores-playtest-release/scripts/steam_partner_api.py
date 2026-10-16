"""Steam publisher API (ISteamApps) の最小クライアント。キーは env.sh の STEAM_WEB_API_KEY から読む。"""
import json
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request

APP_ID = "1958160"
ENV_FILE = "$HOME/hermes-agent/data/services/playtest/env.sh"


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def _key():
    key = subprocess.check_output(["bash", "-c", f'source "{ENV_FILE}"; printf %s "$STEAM_WEB_API_KEY"'], text=True)
    if not key:
        sys.exit("STEAM_WEB_API_KEY が env.sh にありません")
    return key


def call(method, fields, post=False):
    params = urllib.parse.urlencode({"key": _key(), "appid": APP_ID, **fields})
    url = "https://partner.steam-api.com/ISteamApps/" + method
    request = urllib.request.Request(url, data=params.encode()) if post else urllib.request.Request(url + "?" + params)
    try:
        with urllib.request.build_opener(_NoRedirect).open(request, timeout=30) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        sys.exit(f"Steam API HTTP {error.code} ({method})")
    except urllib.error.URLError:
        sys.exit(f"Steam API network failure ({method})")
    if result.get("response", {}).get("result") != 1:
        sys.exit(f"Steam API non-success result ({method}): {json.dumps(result)[:300]}")
    return result
