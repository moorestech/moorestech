// OsInputMac.mm
// macOS 向け OS レベル入力合成ネイティブプラグイン
// macOS native plugin for OS-level synthetic input injection
//
// CGEventPost(kCGHIDEventTap) はフロントのアプリにのみ届くため使用不可。
// CGEventPostToPid(getpid()) で Unity プロセス自身のキューに直接注入する。
// CGEventPost(kCGHIDEventTap) only reaches frontmost app — unusable.
// Use CGEventPostToPid(getpid()) to inject directly into Unity's own event queue.
//
// Accessibility 権限が必要: システム設定 → プライバシーとセキュリティ → アクセシビリティ
// Requires Accessibility permission: System Settings → Privacy & Security → Accessibility

#import <ApplicationServices/ApplicationServices.h>
#include <objc/message.h>
#include <objc/runtime.h>
#include <unistd.h>

extern "C" {

// Unity Editor プロセスを OS レベルで最前面に持ってくる（補助的）
// Bring Unity Editor to OS front (supplementary — main injection uses PostToPid)
void OsInputMac_ActivateApp()
{
    Class nsAppClass = (Class)objc_getClass("NSApplication");
    if (!nsAppClass) return;

    id nsApp = ((id(*)(id, SEL))objc_msgSend)(
        (id)nsAppClass, sel_registerName("sharedApplication"));
    if (!nsApp) return;

    // macOS 14+ では -[NSApplication activate]
    // macOS 14+: use -[NSApplication activate]
    SEL activateSel = sel_registerName("activate");
    if ([nsApp respondsToSelector:activateSel]) {
        ((void(*)(id, SEL))objc_msgSend)(nsApp, activateSel);
    } else {
        SEL oldSel = sel_registerName("activateIgnoringOtherApps:");
        ((void(*)(id, SEL, BOOL))objc_msgSend)(nsApp, oldSel, YES);
    }
}

// Accessibility 権限確認（AXIsProcessTrusted）
// Check Accessibility permission
bool OsInputMac_IsAccessibilityTrusted()
{
    return AXIsProcessTrusted();
}

// Accessibility 権限をシステムダイアログで要求する（AXIsProcessTrustedWithOptions）
// Request Accessibility permission via system dialog
void OsInputMac_RequestAccessibility()
{
    NSDictionary* options = @{(id)kAXTrustedCheckOptionPrompt: @YES};
    AXIsProcessTrustedWithOptions((__bridge CFDictionaryRef)options);
}

// キー注入（CGEventPostToPid → Unity プロセス自身のキューへ直接注入）
// Key injection via CGEventPostToPid → directly into Unity's own event queue
void OsInputMac_Key(uint16_t keyCode, bool isDown)
{
    CGEventRef e = CGEventCreateKeyboardEvent(NULL, (CGKeyCode)keyCode, isDown);
    if (!e) return;

    // フォーカス不要: プロセス自身のイベントキューに直接送る
    // No focus required: send directly to own process event queue
    CGEventPostToPid(getpid(), e);
    CFRelease(e);
}

// マウスの相対移動（CGEventPostToPid）
// Relative mouse movement via CGEventPostToPid
void OsInputMac_MouseMoveBy(double dx, double dy)
{
    CGEventRef locEvent = CGEventCreate(NULL);
    if (!locEvent) return;

    CGPoint p = CGEventGetLocation(locEvent);
    CFRelease(locEvent);

    p.x += dx;
    p.y += dy;

    CGEventRef move = CGEventCreateMouseEvent(NULL, kCGEventMouseMoved, p, kCGMouseButtonLeft);
    if (!move) return;

    CGEventPostToPid(getpid(), move);
    CFRelease(move);
}

// 左クリック（down → up、CGEventPostToPid）
// Left mouse click via CGEventPostToPid
void OsInputMac_MouseLeftClick()
{
    CGEventRef locEvent = CGEventCreate(NULL);
    if (!locEvent) return;

    CGPoint p = CGEventGetLocation(locEvent);
    CFRelease(locEvent);

    CGEventRef down = CGEventCreateMouseEvent(NULL, kCGEventLeftMouseDown, p, kCGMouseButtonLeft);
    CGEventRef up   = CGEventCreateMouseEvent(NULL, kCGEventLeftMouseUp,   p, kCGMouseButtonLeft);

    if (down) { CGEventPostToPid(getpid(), down); CFRelease(down); }
    if (up)   { CGEventPostToPid(getpid(), up);   CFRelease(up); }
}

} // extern "C"
