#!/usr/bin/env python3
"""Unity無しで箱を調べる。
Inspect report boxes without Unity.
"""
import argparse
from pathlib import Path
import sys
from triage_lib import manifest_summary, log_scan, train_timeline, rail_edges, contact_sheet


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('box_dir', type=Path)
    parser.add_argument('--out', required=True, type=Path)
    args = parser.parse_args()
    box, out = args.box_dir.resolve(), args.out.resolve()
    if box == out or box in out.parents:
        parser.error('--out は箱の外を指定してください')
    out.mkdir(parents=True, exist_ok=True)
    # 読めない節は理由を残し、独立した残りの調査を続ける
    # Preserve section read failures while continuing independent investigations
    sections = []
    for module in (manifest_summary, log_scan, train_timeline, rail_edges):
        try:
            sections.extend(module.summarize(box))
        except (OSError, ValueError, KeyError, TypeError, OverflowError) as error:
            print(f'{module.__name__}: {error}', file=sys.stderr)
            sections.extend([f'## {module.__name__.split(".")[-1]}', f'調査失敗: {error}'])
    try:
        sections.extend(log_scan.stalls(box))
    except (OSError, ValueError, TypeError) as error:
        print(f'停滞診断: {error}', file=sys.stderr)
        sections.extend(['## 停滞診断', f'調査失敗: {error}'])
    sections.extend(contact_sheet.summarize(box, out))
    report = '\n'.join(sections) + '\n'
    (out / 'triage.md').write_text(report, encoding='utf-8')
    print(report, end='')


if __name__ == '__main__':
    main()
