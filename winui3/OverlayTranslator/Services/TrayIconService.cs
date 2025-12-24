using OverlayTranslator.Utils;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OverlayTranslator.Services
{
    /// <summary>
    /// タスクトレイアイコン機能を提供するサービス
    /// WinUI 3では直接サポートされていないため、Win32 APIを使用
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private bool _disposed = false;
        private bool _isVisible = false;

        // Win32 API定義
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA pnid);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint LR_DEFAULTSIZE = 0x00000040;

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        public const int NIM_ADD = 0x00000000;
        public const int NIM_MODIFY = 0x00000001;
        public const int NIM_DELETE = 0x00000002;
        public const int NIF_MESSAGE = 0x00000001;
        public const int NIF_ICON = 0x00000002;
        public const int NIF_TIP = 0x00000004;
        public const int WM_TRAYICON = 0x8000;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int IDI_APPLICATION = 32512;
        public const uint MF_STRING = 0x00000000;
        public const uint MF_SEPARATOR = 0x00000800;
        public const uint TPM_LEFTALIGN = 0x0000;
        public const uint TPM_RETURNCMD = 0x0100;

        private const int MENU_ID_SETTINGS = 1;
        private const int MENU_ID_QUIT = 2;

        private IntPtr _windowHandle;
        private int _iconId = 1;
        private IntPtr _iconHandle = IntPtr.Zero;

        public event EventHandler? SettingsClicked;
        public event EventHandler? QuitClicked;

        public TrayIconService(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            
            // icon.icoファイルからアイコンを読み込む
            _iconHandle = LoadIconFromFile();
            
            // ファイルから読み込めなかった場合はデフォルトアイコンを使用
            if (_iconHandle == IntPtr.Zero)
            {
                _iconHandle = LoadIcon(IntPtr.Zero, new IntPtr(IDI_APPLICATION));
                Logger.Warning("icon.icoファイルからアイコンを読み込めませんでした。デフォルトアイコンを使用します。");
            }
            else
            {
                Logger.Info("icon.icoファイルからアイコンを読み込みました");
            }
            
            Logger.Debug("TrayIconServiceを初期化しました");
        }

        /// <summary>
        /// icon.icoファイルからアイコンを読み込む
        /// </summary>
        private IntPtr LoadIconFromFile()
        {
            try
            {
                // icon.icoファイルのパスを取得
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
                
                // ファイルが存在しない場合は、実行ファイルと同じディレクトリを試す
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
                }
                
                if (File.Exists(iconPath))
                {
                    // 絶対パスに変換
                    string absolutePath = Path.GetFullPath(iconPath);
                    
                    // LoadImageを使用してアイコンを読み込む
                    IntPtr hIcon = LoadImage(
                        IntPtr.Zero,
                        absolutePath,
                        IMAGE_ICON,
                        0, 0, // デフォルトサイズ
                        LR_LOADFROMFILE | LR_DEFAULTSIZE
                    );
                    
                    if (hIcon != IntPtr.Zero)
                    {
                        Logger.Info($"アイコンを読み込みました: {absolutePath}");
                        return hIcon;
                    }
                    else
                    {
                        Logger.Warning($"LoadImageが失敗しました: {absolutePath}");
                    }
                }
                else
                {
                    Logger.Warning($"アイコンファイルが見つかりません: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("アイコンの読み込みエラー", ex);
            }
            
            return IntPtr.Zero;
        }

        /// <summary>
        /// タスクトレイアイコンを表示
        /// </summary>
        public void Show()
        {
            if (_isVisible || _windowHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var nid = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _windowHandle,
                    uID = _iconId,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                    uCallbackMessage = WM_TRAYICON,
                    hIcon = _iconHandle,
                    szTip = "Overlay Translator"
                };

                bool result = Shell_NotifyIconW(NIM_ADD, ref nid);
                if (result)
                {
                    _isVisible = true;
                    Logger.Info("タスクトレイアイコンを表示しました");
                }
                else
                {
                    Logger.Error("タスクトレイアイコンの表示に失敗しました");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("タスクトレイアイコンの表示エラー", ex);
            }
        }

        /// <summary>
        /// タスクトレイアイコンを非表示
        /// </summary>
        public void Hide()
        {
            if (!_isVisible || _windowHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var nid = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _windowHandle,
                    uID = _iconId
                };

                bool result = Shell_NotifyIconW(NIM_DELETE, ref nid);
                if (result)
                {
                    _isVisible = false;
                    Logger.Info("タスクトレイアイコンを非表示にしました");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("タスクトレイアイコンの非表示エラー", ex);
            }
        }

        /// <summary>
        /// コンテキストメニューを表示
        /// </summary>
        public void ShowContextMenu()
        {
            try
            {
                var hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero)
                {
                    Logger.Error("コンテキストメニューの作成に失敗しました");
                    return;
                }

                AppendMenu(hMenu, MF_STRING, MENU_ID_SETTINGS, "設定");
                AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
                AppendMenu(hMenu, MF_STRING, MENU_ID_QUIT, "終了");

                // マウス位置を取得
                GetCursorPos(out POINT point);

                uint result = TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_RETURNCMD, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
                
                DestroyMenu(hMenu);

                // メニュー選択を処理
                switch (result)
                {
                    case MENU_ID_SETTINGS:
                        SettingsClicked?.Invoke(this, EventArgs.Empty);
                        break;
                    case MENU_ID_QUIT:
                        QuitClicked?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("コンテキストメニューの表示エラー", ex);
            }
        }

        /// <summary>
        /// タスクトレイアイコンのメッセージを処理
        /// </summary>
        public void ProcessTrayIconMessage(IntPtr lParam)
        {
            int message = lParam.ToInt32();
            switch (message)
            {
                case WM_RBUTTONDOWN:
                    ShowContextMenu();
                    break;
                case WM_LBUTTONDOWN:
                    // 左クリック: 将来の実装（設定ウィンドウを表示など）
                    break;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // マネージドリソースの解放
                }
                // アンマネージドリソースの解放
                Hide();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
