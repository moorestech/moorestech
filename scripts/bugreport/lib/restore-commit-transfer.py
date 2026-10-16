"""Validate and restore transferred commit bundles before publishing or preparing a report."""
import hashlib
import json
import os
from pathlib import Path
import re
import sys


def restore(box):
    box = Path(box).resolve()
    index = box / 'repo/commit-transfer.json'
    if not index.exists():
        return  # silent-ok: 従来箱は分割計画を持たない / 通常のbundle復元が欠損を報告する
    plan = json.loads(index.read_text())
    if not isinstance(plan, list) or not 1 <= len(plan) <= 2:
        raise ValueError('invalid commit transfer plan')
    seen = set()
    part_number = 0
    for bundle in plan:
        relative = bundle.get('path')
        if relative not in ('repo/commits.bundle', 'repo/master-commits.bundle') or relative in seen:
            raise ValueError(f'invalid or duplicate commit bundle path: {relative!r}')
        seen.add(relative)
        if type(bundle.get('bytes')) is not int or bundle['bytes'] <= 0 or not isinstance(bundle.get('parts'), list) or not bundle['parts']:
            raise ValueError(f'invalid commit transfer size or parts: {relative}')
        destination = box / relative
        temporary = destination.with_suffix('.bundle.restoring')
        total = 0
        # 外部の断片を長さ・ハッシュで検証し、完成した資料だけを公開する
        # Verify external parts by length and digest, publishing only complete evidence
        with temporary.open('wb') as output:
            for part in bundle['parts']:
                expected = f'__attachments/{part_number:06d}/chunk'
                part_number += 1
                if part.get('path') != expected or type(part.get('bytes')) is not int or part['bytes'] <= 0:
                    raise ValueError(f'invalid commit transfer part: {part!r}')
                if not isinstance(part.get('sha256'), str) or not re.fullmatch('[0-9a-f]{64}', part['sha256']):
                    raise ValueError(f'invalid commit transfer digest: {expected}')
                path = box / expected
                if path.resolve() != path:
                    raise ValueError(f'commit transfer part escapes through symlink: {path}')
                digest = hashlib.sha256()
                length = 0
                with path.open('rb') as source:
                    while block := source.read(1024 * 1024):
                        length += len(block)
                        if length > part['bytes']:
                            raise ValueError(f'commit transfer part too long: {path}')
                        digest.update(block)
                        output.write(block)
                if length != part['bytes'] or digest.hexdigest() != part['sha256']:
                    raise ValueError(f'commit transfer part length/digest mismatch: {path}')
                total += length
        if total != bundle['bytes']:
            raise ValueError(f'commit transfer bundle length mismatch: {relative}')
        os.replace(temporary, destination)
        print(f'[commit-transfer] restored {relative}: {total} bytes', file=sys.stderr)


if __name__ == '__main__':
    # 外部JSON・ディスク境界。復元失敗をstderrと非zero終了で公開・ACK拒否へ渡す
    # External JSON/disk boundary: stderr and a nonzero exit prevent publication and acknowledgement
    try:
        restore(sys.argv[1])
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        print(f'[commit-transfer] restoration failed: {error}', file=sys.stderr)
        sys.exit(1)
