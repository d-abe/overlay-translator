using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OverlayTranslator.Models;
using OverlayTranslator.Utils;
using System;
using Windows.Graphics;
using Windows.UI;
using WinUIEx;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OverlayTranslator.Services
{
    /// <summary>
    /// オーバーレイ表示機能を提供するサービス
    /// WinUI 3のWindowクラスを使用して透明ウィンドウを作成
    /// </summary>
    public class OverlayService
    {
        private Window? _overlayWindow;
        private TextBlock? _textBlock;
        private Button? _closeButton;
        private Windows.Win32.Foundation.HWND? _windowHandle;
        private IntPtr _originalWndProc;
        private WndProcDelegate? _wndProcDelegate;
        
        // Win32ウィンドウプロシージャのデリゲート
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// オーバーレイウィンドウを表示
        /// </summary>
        public void ShowOverlay(int x, int y, int width, int height, string text, Color bgColor, Settings? settings = null)
        {
            try
            {
                Logger.Info($"オーバーレイ表示: x={x}, y={y}, width={width}, height={height}, text=\"{text}\"");

                // 既存のオーバーレイを閉じる
                CloseOverlay();

                // 新しいオーバーレイウィンドウを作成
                _overlayWindow = new Window();
                var appWindow = _overlayWindow.AppWindow;
                appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
                
                // タスクバーに表示されないようにする
                appWindow.IsShownInSwitchers = false;
                
                // タイトルバーを非表示にする
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                
                // WinUIExを使用してウィンドウの背景を透明にする（矩形選択ウィンドウと同様）
                _overlayWindow.SystemBackdrop = new TransparentTintBackdrop();

                // フォント設定を取得（設定が指定されていない場合はデフォルト値を使用）
                var fontFamily = settings?.OverlayFontFamily ?? "Meiryo";
                var fontSize = settings?.OverlayFontSize ?? 12;
                var fontStyle = settings?.OverlayFontStyle ?? "normal";

                // フォントスタイルを設定
                var fontWeight = Microsoft.UI.Text.FontWeights.Normal;
                var fontStyleObj = Windows.UI.Text.FontStyle.Normal;
                
                if (fontStyle.Contains("bold", StringComparison.OrdinalIgnoreCase))
                {
                    fontWeight = Microsoft.UI.Text.FontWeights.Bold;
                }
                if (fontStyle.Contains("italic", StringComparison.OrdinalIgnoreCase))
                {
                    fontStyleObj = Windows.UI.Text.FontStyle.Italic;
                }

                // 背景色に合わせたテキスト色を計算（コントラストが高い色を選択）
                var textColor = GetContrastColor(bgColor);

                // DPIスケーリングファクターを取得（ScreenCaptureServiceと同じ方法）
                double dpiScale = GetDpiScaleFactor();
                Logger.Info($"オーバーレイ表示: DPIスケーリングファクター={dpiScale}");

                // 論理座標を物理座標に変換（SelectionServiceから渡される座標は論理座標）
                int physicalX = (int)(x * dpiScale);
                int physicalY = (int)(y * dpiScale);
                int physicalWidth = (int)(width * dpiScale);
                int physicalHeight = (int)(height * dpiScale);
                
                Logger.Info($"オーバーレイ表示: 論理座標 x={x}, y={y}, width={width}, height={height}");
                Logger.Info($"オーバーレイ表示: 物理座標 x={physicalX}, y={physicalY}, width={physicalWidth}, height={physicalHeight}");

                // ×ボタンを作成（文字色と同じ色、右上に配置）
                _closeButton = new Button
                {
                    Content = "×",
                    Foreground = new SolidColorBrush(textColor),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Width = 30,
                    Height = 30,
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                    VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
                    Margin = new Microsoft.UI.Xaml.Thickness(5),
                    Padding = new Microsoft.UI.Xaml.Thickness(0),
                    BorderThickness = new Microsoft.UI.Xaml.Thickness(0)
                };
                _closeButton.Click += (s, e) =>
                {
                    Logger.Info("×ボタンがクリックされました");
                    CloseOverlay();
                };

                // テキストブロックを作成
                // 元の画像の色に近い背景に対して、コントラストの高いテキスト色を使用
                _textBlock = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(textColor),
                    FontSize = fontSize,
                    FontFamily = new FontFamily(fontFamily),
                    FontWeight = fontWeight,
                    FontStyle = fontStyleObj,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap, // 横方向は折り返し（固定幅）
                    Padding = new Microsoft.UI.Xaml.Thickness(10),
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 0), // ScrollViewerのMarginで調整
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
                    VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top, // 上から開始（垂直中央にしない）
                    MaxWidth = physicalWidth - 20 // 横方向の最大幅を制限（パディングを考慮）
                };

                // 背景色のブラシを作成（切り取った元の画像の色に近い背景色かつ透過）
                // キャプチャした画像の主要色を使用し、半透明にする
                var overlayBgBrush = new SolidColorBrush(bgColor)
                {
                    Opacity = 0.8 // 元の画像の色を保ちつつ、透過させる
                };

                // ScrollViewerでテキストを囲む（縦スクロールのみ、横は固定）
                var scrollViewer = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled, // 横スクロールは無効
                    VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto, // 縦スクロールは自動
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 35, 0, 0) // ×ボタンの下にマージンを追加
                };
                scrollViewer.Content = _textBlock;

                var grid = new Grid
                {
                    Background = overlayBgBrush
                };
                grid.Children.Add(_closeButton);
                grid.Children.Add(scrollViewer);

                _overlayWindow.Content = grid;

                // ウィンドウハンドルを取得（先に取得する必要がある）
                var hwnd = Utils.WindowHelper.GetWindowHandle(_overlayWindow);
                
                // ウィンドウを一度表示してからテキストの高さを測定
                _overlayWindow.Activate();
                
                // ウィンドウが非アクティブになったら閉じる
                _overlayWindow.Activated += OnWindowActivated;
                appWindow.Closing += OnWindowClosing;
                
                // Win32メッセージをフックして、WM_ACTIVATEで非アクティブを検出
                if (hwnd != IntPtr.Zero)
                {
                    HookWindowMessages(hwnd);
                }
                
                // レイアウトを更新してテキストのサイズを測定（DispatcherQueueを使用）
                _overlayWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    // テキストブロックの実際のサイズを取得
                    _textBlock.Measure(new Windows.Foundation.Size(physicalWidth - 20, double.PositiveInfinity)); // パディングを考慮
                    _textBlock.Arrange(new Windows.Foundation.Rect(0, 0, physicalWidth - 20, _textBlock.DesiredSize.Height));
                    
                    double textHeight = _textBlock.DesiredSize.Height;
                    double buttonAndPadding = 35 + 20; // ×ボタンの高さ + マージン + パディング
                    double textRequiredHeight = textHeight + buttonAndPadding;
                    
                    // 矩形選択の高さを最小値として使用（テキストが短い場合でも矩形選択の高さを維持）
                    // テキストが矩形選択の高さを超える場合は、テキストの高さを使用
                    double requiredHeight = Math.Max(physicalHeight, textRequiredHeight);
                    
                    // 画面の高さを超えないように制限
                    int screenHeight = Windows.Win32.PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYSCREEN);
                    int maxHeight = screenHeight - physicalY;
                    int finalHeight = (int)Math.Min(requiredHeight, maxHeight);
                    
                    Logger.Info($"矩形選択高さ: {physicalHeight}, テキスト高さ: {textHeight}, テキスト必要高さ: {textRequiredHeight}, 必要高さ: {requiredHeight}, 最終高さ: {finalHeight}");

                    // ウィンドウの位置とサイズを設定（物理座標を使用、高さを調整）
                    appWindow.MoveAndResize(new RectInt32(physicalX, physicalY, physicalWidth, finalHeight));
                });

                // ウィンドウを最前面に表示し、タイトルバーを完全に非表示にする（Win32 APIを使用）
                if (hwnd != IntPtr.Zero)
                {
                    unsafe
                    {
                        // タイトルバーを非表示にする（WS_OVERLAPPEDWINDOWからタイトルバーを削除）
                        const long WS_CAPTION = 0x00C00000;
                        const long WS_THICKFRAME = 0x00040000;
                        const long WS_SYSMENU = 0x00080000;
                        
                        var windowHwnd = new Windows.Win32.Foundation.HWND(hwnd);
                        var style = Windows.Win32.PInvoke.GetWindowLongPtr(windowHwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                        var newStyle = new IntPtr((long)style & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU));
                        Windows.Win32.PInvoke.SetWindowLongPtr(windowHwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE, newStyle);
                        
                        // ウィンドウを最前面に表示（高さ調整後に再設定）
                        Windows.Win32.PInvoke.SetWindowPos(
                            windowHwnd,
                            new Windows.Win32.Foundation.HWND(new IntPtr(-1)), // HWND_TOPMOST
                            0, 0, 0, 0,
                            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE | 
                            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE | 
                            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | 
                            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED
                        );
                    }
                }
                
                Logger.Info("オーバーレイウィンドウを表示しました");
            }
            catch (Exception ex)
            {
                Logger.Error("オーバーレイ表示エラー", ex);
            }
        }

        /// <summary>
        /// ウィンドウがアクティブになったときの処理
        /// </summary>
        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            // アクティブになったときは何もしない
        }

        /// <summary>
        /// ウィンドウが閉じられるときの処理
        /// </summary>
        private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            // イベントハンドラーを解除
            if (_overlayWindow != null)
            {
                _overlayWindow.Activated -= OnWindowActivated;
            }
            
            // Win32メッセージフックを解除
            UnhookWindowMessages();
        }

        /// <summary>
        /// Win32メッセージをフックして、非アクティブを検出
        /// </summary>
        private unsafe void HookWindowMessages(IntPtr hwnd)
        {
            try
            {
                _windowHandle = new Windows.Win32.Foundation.HWND(hwnd);
                
                // ウィンドウプロシージャをフック
                _originalWndProc = Windows.Win32.PInvoke.GetWindowLongPtr(_windowHandle.Value, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC);
                _wndProcDelegate = new WndProcDelegate(WndProc);
                Windows.Win32.PInvoke.SetWindowLongPtr(_windowHandle.Value, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
                
                Logger.Debug("オーバーレイウィンドウのメッセージフックを設定しました");
            }
            catch (Exception ex)
            {
                Logger.Warning($"メッセージフックの設定に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// Win32メッセージフックを解除
        /// </summary>
        private unsafe void UnhookWindowMessages()
        {
            if (_windowHandle.HasValue && _originalWndProc != IntPtr.Zero)
            {
                try
                {
                    Windows.Win32.PInvoke.SetWindowLongPtr(_windowHandle.Value, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, _originalWndProc);
                    _windowHandle = null;
                    _originalWndProc = IntPtr.Zero;
                    _wndProcDelegate = null;
                    Logger.Debug("オーバーレイウィンドウのメッセージフックを解除しました");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"メッセージフックの解除に失敗しました: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ウィンドウプロシージャ
        /// </summary>
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            const uint WM_ACTIVATE = 0x0006;
            const uint WA_INACTIVE = 0;
            
            // WM_ACTIVATEメッセージを処理
            if (msg == WM_ACTIVATE)
            {
                uint wParamLow = (uint)(wParam.ToInt64() & 0xFFFF);
                if (wParamLow == WA_INACTIVE)
                {
                    // ウィンドウが非アクティブになった
                    Logger.Info("オーバーレイウィンドウが非アクティブになりました。閉じます。");
                    
                    // UIスレッドで閉じる
                    _overlayWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        CloseOverlay();
                    });
                    
                    return IntPtr.Zero;
                }
            }
            
            // デフォルトのウィンドウプロシージャを呼び出す
            if (_originalWndProc != IntPtr.Zero)
            {
                return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
            }
            
            return IntPtr.Zero;
        }
        
        // Win32 APIのP/Invoke定義
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// DPIスケーリングファクターを取得
        /// ScreenCaptureServiceと同じ方法を使用
        /// </summary>
        private static double GetDpiScaleFactor()
        {
            try
            {
                // プライマリモニターのデバイスコンテキストを取得
                Windows.Win32.Graphics.Gdi.HDC hdc = Windows.Win32.PInvoke.GetDC(Windows.Win32.Foundation.HWND.Null);
                try
                {
                    // LOGPIXELSX = 88 (GetDeviceCapsの定数)
                    const int LOGPIXELSX = 88;
                    int dpiX = Windows.Win32.PInvoke.GetDeviceCaps(hdc, (Windows.Win32.Graphics.Gdi.GET_DEVICE_CAPS_INDEX)LOGPIXELSX);
                    
                    // 標準DPI (96) に対するスケーリングファクターを計算
                    double scale = dpiX / 96.0;
                    Logger.Info($"GetDeviceCapsから取得したDPI: {dpiX}, スケール: {scale}");
                    return scale;
                }
                finally
                {
                    Windows.Win32.PInvoke.ReleaseDC(Windows.Win32.Foundation.HWND.Null, hdc);
                }
            }
            catch (Exception ex)
            {
                // エラー時は1.0を返す（スケーリングなし）
                Logger.Warning($"DPIスケールの取得に失敗しました: {ex.Message}。デフォルト値1.0を使用します。");
                return 1.0;
            }
        }


        /// <summary>
        /// 背景色に合わせたコントラストの高いテキスト色を取得
        /// </summary>
        private Color GetContrastColor(Color bgColor)
        {
            // 輝度を計算（0.299*R + 0.587*G + 0.114*B）
            double luminance = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255.0;
            
            // 輝度が0.5以上なら黒、それ以下なら白
            if (luminance > 0.5)
            {
                return Microsoft.UI.Colors.Black;
            }
            else
            {
                return Microsoft.UI.Colors.White;
            }
        }

        /// <summary>
        /// オーバーレイウィンドウを閉じる
        /// </summary>
        public void CloseOverlay()
        {
            // Win32メッセージフックを解除
            UnhookWindowMessages();
            
            if (_overlayWindow != null)
            {
                // イベントハンドラーを解除
                _overlayWindow.Activated -= OnWindowActivated;
                if (_overlayWindow.AppWindow != null)
                {
                    _overlayWindow.AppWindow.Closing -= OnWindowClosing;
                }
                
                _overlayWindow.Close();
                _overlayWindow = null;
                _textBlock = null;
                _closeButton = null;
                Logger.Info("オーバーレイウィンドウを閉じました");
            }
        }
    }
}

