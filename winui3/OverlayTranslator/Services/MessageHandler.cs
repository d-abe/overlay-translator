using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using OverlayTranslator.Utils;

namespace OverlayTranslator.Services
{
    /// <summary>
    /// Win32メッセージを処理するクラス
    /// WinUI 3アプリでWin32 APIのメッセージ（WM_TRAYICON、WM_HOTKEYなど）を処理する
    /// </summary>
    public class MessageHandler
    {
        // Win32メッセージ定数
        public const int WM_TRAYICON = 0x8000;
        public const int WM_HOTKEY = 0x0312;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_LBUTTONDOWN = 0x0201;

        private readonly Window _window;
        private readonly TrayIconService? _trayIconService;
        private readonly HotkeyService? _hotkeyService;

        public MessageHandler(Window window, TrayIconService? trayIconService = null, HotkeyService? hotkeyService = null)
        {
            _window = window;
            _trayIconService = trayIconService;
            _hotkeyService = hotkeyService;

            // ウィンドウのメッセージループにフック
            // WinUI 3では、Win32メッセージを処理するために特別な処理が必要
            // 現時点では、基本的な実装のみ
            Logger.Debug("MessageHandlerを初期化しました");
        }

        /// <summary>
        /// Win32メッセージを処理
        /// </summary>
        public IntPtr ProcessMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_TRAYICON:
                    if (_trayIconService != null)
                    {
                        // タスクトレイアイコンのメッセージを処理
                        if (lParam.ToInt32() == WM_RBUTTONDOWN)
                        {
                            // 右クリック: コンテキストメニューを表示
                            _trayIconService.ShowContextMenu();
                        }
                        else if (lParam.ToInt32() == WM_LBUTTONDOWN)
                        {
                            // 左クリック: 設定ウィンドウを表示（将来の実装）
                        }
                        handled = true;
                    }
                    break;

                case WM_HOTKEY:
                    if (_hotkeyService != null)
                    {
                        // ホットキーのメッセージを処理
                        int hotkeyId = wParam.ToInt32();
                        _hotkeyService.ProcessHotkeyMessage(hotkeyId);
                        handled = true;
                    }
                    break;
            }

            return IntPtr.Zero;
        }
    }
}

