import { useToastStore } from "../features/toast/toastStore";

// 画面右下にトーストを表示するホスト。自動消滅は store 側（addToast）で管理
// Toast host pinned to the bottom-right; auto-dismiss is handled in the store (addToast)
export default function ToastHost() {
  const toasts = useToastStore((s) => s.toasts);

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
