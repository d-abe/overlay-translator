using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using OverlayTranslator.Services;

namespace OverlayTranslator.Utils
{
    /// <summary>
    /// WinUI 3ウィンドウでWin32メッセージを処理するヘルパークラス
    /// </summary>
    public class WindowMessageHandler
    {
        private const int GWLP_WNDPROC = -4;
        private const int WM_TRAYICON = 0x8000;
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        private IntPtr _originalWndProc;
        private IntPtr _windowHandle;
        private TrayIconService? _trayIconService;
        private HotkeyService? _hotkeyService;
        private WndProcDelegate? _wndProcDelegate; // ガベージコレクションを防ぐために保持

        public WindowMessageHandler(Window window, TrayIconService? trayIconService = null, HotkeyService? hotkeyService = null)
        {
            _trayIconService = trayIconService;
            _hotkeyService = hotkeyService;

            // ウィンドウハンドルを取得
            _windowHandle = WindowHelper.GetWindowHandle(window);
            if (_windowHandle == IntPtr.Zero)
            {
                Logger.Error("ウィンドウハンドルの取得に失敗しました");
                return;
            }

            // ウィンドウプロシージャをフック
            _originalWndProc = GetWindowLongPtr(_windowHandle, GWLP_WNDPROC);
            _wndProcDelegate = new WndProcDelegate(WndProc); // デリゲートを保持
            var newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, newWndProc);

            Logger.Debug("WindowMessageHandlerを初期化しました");
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_TRAYICON:
                    if (_trayIconService != null)
                    {
                        _trayIconService.ProcessTrayIconMessage(lParam);
                        return IntPtr.Zero;
                    }
                    break;

                case WM_HOTKEY:
                    if (_hotkeyService != null)
                    {
                        int hotkeyId = wParam.ToInt32();
                        _hotkeyService.ProcessHotkeyMessage(hotkeyId);
                        return IntPtr.Zero;
                    }
                    break;
            }

            // デフォルトのウィンドウプロシージャを呼び出す
            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}

