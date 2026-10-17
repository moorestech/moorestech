"""Read deployment environment without interpolating shell source or exposing values."""
import os
from pathlib import Path
import subprocess


NAMES = ('MOORESTECH_STEAM_USER', 'MOORESTECH_STEAM_STATE_ROOT',
         'MOORESTECH_STEAMCMD_EXECUTABLE', 'MOORESTECH_STEAM_OP_HELPER', 'MOORESTECH_STEAM_OP_ITEM')


class MissingEnvironmentError(ValueError):
    def __init__(self, name):
        super().__init__("required environment setting is missing")
        self.name = name


def configure():
    filename = os.environ.get('MOORESTECH_PLAYTEST_ENV_FILE')
    if filename is None:
        filename = str(Path.home() / 'hermes-agent/data/services/playtest/env.sh')
    # 固定プログラムにパスだけを引数で渡し、秘密値を出力先へ転送しない。
    # Pass only the path to fixed shell code and keep captured values out of reports.
    script = 'set -e; source "$1"; shift; for name in "$@"; do printf "%s\\0" "${!name-}"; done'
    result = subprocess.run(['/bin/bash', '-c', script, 'playtest-env', filename, *NAMES],
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode:
        raise ValueError('deployment environment could not be loaded')
    values = result.stdout.decode().split('\0')
    for name, value in zip(NAMES, values):
        os.environ[name] = value
    for name in NAMES[:2]:
        required(name)


def required(name):
    value = os.environ.get(name)
    if not value:
        raise MissingEnvironmentError(name)
    return value
