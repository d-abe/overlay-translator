using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace OverlayTranslator.Utils
{
    /// <summary>
    /// WinUI 3ウィンドウからWin32 API用のハンドルを取得するヘルパークラス
    /// </summary>
    public static class WindowHelper
    {
        /// <summary>
        /// WinUI 3のWindowからウィンドウハンドル（HWND）を取得
        /// </summary>
        public static IntPtr GetWindowHandle(Microsoft.UI.Xaml.Window window)
        {
            if (window == null)
            {
                return IntPtr.Zero;
            }

            try
            {
                // WinRT.Interop.WindowNative.GetWindowHandleを使用
                var windowHandle = WindowNative.GetWindowHandle(window);
                return windowHandle;
            }
            catch (Exception ex)
            {
                Logger.Error("ウィンドウハンドルの取得に失敗しました", ex);
                return IntPtr.Zero;
            }
        }
    }
}

