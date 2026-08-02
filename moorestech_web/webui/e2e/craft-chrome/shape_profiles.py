"""Shared detection and profile helpers for craft-chrome image checks."""

import numpy as np

GRIP_ZONE_SIZE = 80
GRIP_CHANNEL_SPREAD = 35
GRIP_MIN_LUMINANCE = 70
GRIP_MAX_LUMINANCE = 190
GRIP_MIN_DIMENSION = 5
FRAME_DARK_THRESHOLD = 70
PIXEL_CONNECTIVITY_RADIUS = 1
HAMMER_CHANNEL_SPREAD = 15
HAMMER_MIN_LUMINANCE = 68
HAMMER_MAX_LUMINANCE = 100
HAMMER_CONNECTIVITY_RADIUS = 3
SIDE_COLOR = np.array((16, 15, 21), dtype=np.uint8)


def components(mask: np.ndarray, x0: int, y0: int, radius: int) -> list[tuple[int, int, int, int, int]]:
    seen = np.zeros(mask.shape, dtype=bool)
    found = []
    for y, x in np.argwhere(mask):
        if seen[y, x]:
            continue
        stack, seen[y, x] = [(int(y), int(x))], True
        count, min_x, min_y, max_x, max_y = 0, x, y, x, y
        while stack:
            cy, cx = stack.pop()
            count, min_x, min_y = count + 1, min(min_x, cx), min(min_y, cy)
            max_x, max_y = max(max_x, cx), max(max_y, cy)
            for ny in range(cy - radius, cy + radius + 1):
                for nx in range(cx - radius, cx + radius + 1):
                    if 0 <= ny < mask.shape[0] and 0 <= nx < mask.shape[1] and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        found.append((int(min_x + x0), int(min_y + y0), int(max_x + x0), int(max_y + y0), count))
    return found


def detect_frame(dark: np.ndarray) -> np.ndarray:
    frame = np.zeros(dark.shape, dtype=bool)
    edge = np.zeros(dark.shape, dtype=bool)
    edge[-1, :], edge[:, -1] = True, True
    stack = [(int(y), int(x)) for y, x in np.argwhere(dark & edge)]
    while stack:
        y, x = stack.pop()
        if frame[y, x]:
            continue
        frame[y, x] = True
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < dark.shape[0] and 0 <= nx < dark.shape[1] and dark[ny, nx] and not frame[ny, nx]:
                stack.append((ny, nx))
    return frame


def touches_frame(mask: np.ndarray, frame: np.ndarray, left: int, top: int, right: int, bottom: int, x0: int, y0: int) -> bool:
    candidate = mask[top - y0:bottom - y0 + 1, left - x0:right - x0 + 1]
    expanded = np.pad(frame, 1)[top - y0:bottom - y0 + 3, left - x0:right - x0 + 3]
    nearby = expanded[:-2, :-2] | expanded[:-2, 1:-1] | expanded[:-2, 2:] | expanded[1:-1, :-2] | expanded[1:-1, 2:] | expanded[2:, :-2] | expanded[2:, 1:-1] | expanded[2:, 2:]
    return bool((candidate & nearby).any())


def grip_zone(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[np.ndarray, np.ndarray, np.ndarray, int, int]:
    # グリップ候補の彩度・明度条件を一箇所へ固定する
    # Keep the grip candidate saturation and luminance contract in one place
    _, _, right, bottom = panel
    x0, y0 = right - GRIP_ZONE_SIZE, bottom - GRIP_ZONE_SIZE
    zone = image[y0:bottom + 1, x0:right + 1]
    mask = (zone.max(axis=2) - zone.min(axis=2) < GRIP_CHANNEL_SPREAD) & (zone.mean(axis=2) >= GRIP_MIN_LUMINANCE) & (zone.mean(axis=2) <= GRIP_MAX_LUMINANCE)
    frame = detect_frame(zone.max(axis=2) < FRAME_DARK_THRESHOLD)
    return zone, mask, frame, x0, y0


def grip_component_candidates(image: np.ndarray, panel: tuple[int, int, int, int]) -> list[tuple[int, int, int, int, int]]:
    _, mask, frame, x0, y0 = grip_zone(image, panel)
    candidates = []
    for component in components(mask, x0, y0, PIXEL_CONNECTIVITY_RADIUS):
        left, top, right, bottom, _ = component
        if not touches_frame(mask, frame, left, top, right, bottom, x0, y0) and right - left + 1 >= GRIP_MIN_DIMENSION and bottom - top + 1 >= GRIP_MIN_DIMENSION:
            candidates.append(component)
    return candidates


def detect_grip(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    candidates = grip_component_candidates(image, panel)
    if not candidates:
        raise ValueError("corner grip was not detected")
    return max(candidates, key=lambda item: item[4])[:4]


def hammer_mask(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[np.ndarray, int, int]:
    left, top, _, _ = panel
    x0, y0, x1, y1 = left - 8, top - 100, left + 191, top
    zone = image[y0:y1, x0:x1]
    return (zone.max(axis=2) - zone.min(axis=2) < HAMMER_CHANNEL_SPREAD) & (zone.mean(axis=2) >= HAMMER_MIN_LUMINANCE) & (zone.mean(axis=2) <= HAMMER_MAX_LUMINANCE), x0, y0


def detect_hammer(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[int, int, int, int, int]:
    left, _, _, _ = panel
    mask, x0, y0 = hammer_mask(image, panel)
    candidates = [item for item in components(mask, x0, y0, HAMMER_CONNECTIVITY_RADIUS) if left + 35 <= (item[0] + item[2]) / 2 <= left + 110]
    if not candidates:
        raise ValueError("tab hammer was not detected")
    return max(candidates, key=lambda item: item[4])


def side_components(image: np.ndarray, panel: tuple[int, int, int, int]) -> tuple[tuple[int, int, int, int, int], tuple[int, int, int, int, int], np.ndarray, int, int]:
    # 単色Sideを左右の独立成分として取得する
    # Get the solid Side as independent left and right components
    left, top, _, _ = panel
    x0, y0, x1, y1 = left - 8, top - 100, left + 191, top
    mask = np.all(image[y0:y1, x0:x1] == SIDE_COLOR, axis=2)
    candidates = components(mask, x0, y0, PIXEL_CONNECTIVITY_RADIUS)
    left_candidates = [item for item in candidates if (item[0] + item[2]) / 2 < left + 50]
    right_candidates = [item for item in candidates if (item[0] + item[2]) / 2 > left + 90]
    if not left_candidates or not right_candidates:
        raise ValueError("tab Side components were not detected")
    return max(left_candidates, key=lambda item: item[4]), max(right_candidates, key=lambda item: item[4]), mask, x0, y0


def row_endpoints(mask: np.ndarray, component: tuple[int, int, int, int, int], x0: int, y0: int, panel: tuple[int, int, int, int]) -> dict[int, tuple[int, int]]:
    # 成分の各行端点をパネル原点の正規化座標で返す
    # Return component row endpoints in panel-origin normalized coordinates
    left, top, right, bottom, _ = component
    profile = {}
    for y in range(top, bottom + 1):
        xs = np.flatnonzero(mask[y - y0, left - x0:right - x0 + 1])
        if len(xs):
            profile[y - panel[1]] = (int(left + xs[0] - panel[0]), int(left + xs[-1] - panel[0]))
    return profile


def row_endpoint_delta(reference: dict[int, tuple[int, int]], current: dict[int, tuple[int, int]]) -> int:
    # SVGのクリップによる末端一行差を許容しつつ中央形状を固定する
    # Allow one clipped SVG edge row while locking the central shape
    candidates = []
    for shift in (-1, 0, 1):
        shifted = {row + shift: endpoints for row, endpoints in current.items()}
        shared_rows = reference.keys() & shifted.keys()
        endpoint_delta = max((max(abs(a - b) for a, b in zip(reference[row], shifted[row])) for row in shared_rows), default=0)
        candidates.append(max(endpoint_delta, len(reference.keys() ^ shifted.keys())))
    return min(candidates)
