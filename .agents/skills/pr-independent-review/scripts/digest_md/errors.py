# digest.mdパース全体で共有する例外型
# Exception type shared across every digest.md parsing module
from __future__ import annotations


class DigestError(Exception):
    pass
