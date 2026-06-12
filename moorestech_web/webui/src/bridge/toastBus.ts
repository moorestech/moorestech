// トースト通知の極小 pub-sub。React 外（bridge層）からも emit できるようにする
// Minimal pub-sub for toast notifications, emittable from outside React (bridge layer)

type ToastListener = (message: string) => void;

const listeners = new Set<ToastListener>();

export function subscribeToast(listener: ToastListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function showToast(message: string) {
  listeners.forEach((l) => l(message));
}
