import { useEffect, useState } from "react";
import { subscribeToast } from "../bridge/toastBus";

type Toast = { id: number; message: string };

// 画面右下にトーストを表示するホスト。3秒で自動消滅
// Toast host pinned to the bottom-right; each toast auto-dismisses after 3s
export default function ToastHost() {
  const [toasts, setToasts] = useState<Toast[]>([]);

  useEffect(() => {
    let nextId = 1;
    return subscribeToast((message) => {
      const id = nextId++;
      setToasts((prev) => [...prev, { id, message }]);
      setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 3000);
    });
  }, []);

  return (
    <div className="fixed bottom-4 right-4 space-y-2 z-50">
      {toasts.map((t) => (
        <div key={t.id} className="bg-red-800 text-white text-sm rounded px-3 py-2 shadow">
          {t.message}
        </div>
      ))}
    </div>
  );
}
