import shutil
import subprocess
import sys


def summarize(box, out):
    lines = ['## 動画']
    video = box / 'video.mp4'
    if not video.is_file():
        return lines + ['video.mp4 が無いためコンタクトシート未生成']
    ffmpeg = shutil.which('ffmpeg')
    if ffmpeg is None:
        return lines + ['ffmpeg が無いためコンタクトシート未生成']
    # 動画という外部入力の変換失敗を結果へ明示する
    # Surface conversion failures from external video input in the result
    result = subprocess.run([ffmpeg, '-y', '-i', str(video), '-vf',
                             'fps=1/5,scale=320:-1,tile=4x4', '-frames:v', '1',
                             str(out / 'contact-sheet.png')], capture_output=True, text=True)
    if result.returncode != 0:
        print('ffmpeg 失敗: ' + result.stderr[-2000:], file=sys.stderr)
        return lines + ['ffmpeg 失敗: ' + result.stderr[-2000:]]
    if not (out / 'contact-sheet.png').is_file():
        return lines + ['動画に対象フレームが無くコンタクトシート未生成']
    return lines + ['contact-sheet.png を生成']
