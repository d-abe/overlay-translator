using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using OverlayTranslator.Utils;
using OverlayTranslator.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUIEx;
using Microsoft.UI.Dispatching;

namespace OverlayTranslator.Services
{
    /// <summary>
    /// 矩形選択機能を提供するサービス
    /// 全画面をカバーする透明ウィンドウで矩形選択を行う
    /// </summary>
    public class SelectionService : IDisposable
    {
        private Window? _selectionWindow;
        private Canvas? _canvas;
        private Rectangle? _selectionRectangle;
        private bool _isSelecting = false;
        private double _startX = 0;
        private double _startY = 0;
        private HWND? _windowHandle;
        private const int ESC_HOTKEY_ID = 9999; // ESCキー用のホットキーID
        private WNDPROC? _originalWndProc;
        private WNDPROC? _wndProcDelegate; // ガベージコレクションを防ぐために保持

        public event Action<int, int, int, int>? OnSelectionCompleted;
        public event Action? OnSelectionCancelled;

        /// <summary>
        /// 矩形選択を開始
        /// </summary>
        public void StartSelection()
        {
            try
            {
                Logger.Info("矩形選択を開始します");

                // 既存の選択ウィンドウを閉じる
                CloseSelection();

                // 画面サイズを取得（Win32 APIを使用）
                int screenWidth = PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXSCREEN);
                int screenHeight = PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYSCREEN);
                
                // マルチモニター対応：仮想画面サイズを取得
                int virtualWidth = PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
                int virtualHeight = PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
                
                // 仮想画面サイズが大きい場合はそれを使用
                if (virtualWidth > screenWidth)
                {
                    screenWidth = virtualWidth;
                }
                if (virtualHeight > screenHeight)
                {
                    screenHeight = virtualHeight;
                }

                Logger.Debug($"画面サイズ: {screenWidth}x{screenHeight}");

                       // 透明ウィンドウを作成
                       _selectionWindow = new Window();
                       var appWindow = _selectionWindow.AppWindow;
                       
                       // タスクバーに表示されないようにする
                       appWindow.IsShownInSwitchers = false;
                       
                       // タイトルバーを完全に非表示にする
                       appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                       appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                       appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                
                // WinUIExを使用してウィンドウの背景を完全に透明にする
                _selectionWindow.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
                
                // ウィンドウの位置とサイズを設定
                appWindow.MoveAndResize(new RectInt32(0, 0, (int)screenWidth, (int)screenHeight));
                
                // Win32 APIを使用してウィンドウを透明にし、最前面に表示し、タイトルバーを非表示にする
                var hwnd = WindowHelper.GetWindowHandle(_selectionWindow);
                if (hwnd != IntPtr.Zero)
                {
                    _windowHandle = new HWND(hwnd);
                    
                    unsafe
                    {
                        // タイトルバーを非表示にする（WS_OVERLAPPEDWINDOWからタイトルバーを削除）
                        const long WS_CAPTION = 0x00C00000;
                        const long WS_THICKFRAME = 0x00040000;
                        const long WS_SYSMENU = 0x00080000;
                        
                        var style = PInvoke.GetWindowLongPtr(_windowHandle.Value, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                        var newStyle = new IntPtr((long)style & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU));
                        PInvoke.SetWindowLongPtr(_windowHandle.Value, WINDOW_LONG_PTR_INDEX.GWL_STYLE, newStyle);
                        
                        // ウィンドウを最前面に表示
                        PInvoke.SetWindowPos(
                            _windowHandle.Value,
                            new HWND(new IntPtr(-1)), // HWND_TOPMOST
                            0, 0, 0, 0,
                            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED
                        );
                        
                        // ウィンドウプロシージャをフックしてWM_HOTKEYメッセージを処理
                        HookWindowMessages(_windowHandle.Value);
                        
                        // ESCキーをグローバルホットキーとして登録（フォーカスがなくても検出できるように）
                        // VK_ESCAPE = 0x1B
                        // 修飾キーなし = 0
                        bool escRegistered = PInvoke.RegisterHotKey(
                            _windowHandle.Value,
                            ESC_HOTKEY_ID,
                            0, // 修飾キーなし
                            0x1B // VK_ESCAPE
                        );
                        Logger.Info($"ESCキーのグローバルホットキー登録: {escRegistered}");
                    }
                }
                
                // キャンバスを作成（全画面をカバー）
                // CrosshairCanvasを使用して十字カーソルを設定
                _canvas = new CrosshairCanvas
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Black)
                    {
                        Opacity = 0.3 // 半透明
                    },
                    Width = screenWidth,
                    Height = screenHeight
                };
                
                
                // ウィンドウの背景を透明にする（RootGridの背景を透明に）
                var rootGrid = new Grid
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                };
                rootGrid.Children.Add(_canvas);

                // 選択矩形用のRectangleを作成（初期は非表示）
                _selectionRectangle = new Rectangle
                {
                    Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 5 },
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Visibility = Visibility.Collapsed
                };

                _canvas.Children.Add(_selectionRectangle);

                // マウスイベントをバインド
                _canvas.PointerPressed += OnPointerPressed;
                _canvas.PointerMoved += OnPointerMoved;
                _canvas.PointerReleased += OnPointerReleased;
                _canvas.KeyDown += OnKeyDown;
                _canvas.KeyUp += OnKeyUp; // キーが離されたときもログ出力
                
                // マウスカーソルを十字に変更
                _canvas.PointerEntered += OnPointerEntered;
                _canvas.PointerExited += OnPointerExited;

                       _selectionWindow.Content = rootGrid;
                _selectionWindow.Activate();

                // Win32 APIを使用してウィンドウを強制的にアクティブにしてフォーカスを取得
                if (hwnd != IntPtr.Zero)
                {
                    unsafe
                    {
                        var windowHwnd = new HWND(hwnd);
                        
                        // ウィンドウを前面に表示
                        PInvoke.SetForegroundWindow(windowHwnd);
                        
                        // ウィンドウを最前面に
                        PInvoke.BringWindowToTop(windowHwnd);
                        
                        // ウィンドウをアクティブにする
                        PInvoke.SetActiveWindow(windowHwnd);
                        
                        // ウィンドウにフォーカスを設定
                        PInvoke.SetFocus(windowHwnd);
                        
                        Logger.Debug("ウィンドウを強制的にアクティブにしました");
                    }
                }

                // キャンバスにフォーカスを設定
                _canvas.Focus(FocusState.Programmatic);
                
                // 少し遅延してから再度フォーカスを設定（確実にフォーカスを取得するため）
                _selectionWindow.DispatcherQueue.TryEnqueue(
                    Microsoft.UI.Dispatching.DispatcherQueuePriority.High,
                    () =>
                    {
                        _canvas?.Focus(FocusState.Programmatic);
                        Logger.Debug("キャンバスにフォーカスを再設定しました");
                    });

                Logger.Info("矩形選択ウィンドウを表示しました");
            }
            catch (Exception ex)
            {
                Logger.Error("矩形選択の開始エラー", ex);
            }
        }

        /// <summary>
        /// 矩形選択をキャンセル
        /// </summary>
        public void CancelSelection()
        {
            Logger.Info("矩形選択をキャンセルします");
            CloseSelection();
            OnSelectionCancelled?.Invoke();
        }

        /// <summary>
        /// 選択ウィンドウを閉じる
        /// </summary>
        private void CloseSelection()
        {
            if (_selectionWindow != null)
            {
                // メッセージフックを解除
                UnhookWindowMessages();
                
                // ESCキーのグローバルホットキーを解除
                if (_windowHandle.HasValue)
                {
                    unsafe
                    {
                        bool unregistered = PInvoke.UnregisterHotKey(_windowHandle.Value, ESC_HOTKEY_ID);
                        Logger.Debug($"ESCキーのグローバルホットキー解除: {unregistered}");
                    }
                    _windowHandle = null;
                }
                
                // イベントハンドラーを解除
                if (_canvas != null)
                {
                    _canvas.KeyDown -= OnKeyDown;
                    _canvas.KeyUp -= OnKeyUp;
                }
                
                _selectionWindow.Close();
                _selectionWindow = null;
                _canvas = null;
                _selectionRectangle = null;
                _isSelecting = false;
                Logger.Debug("選択ウィンドウを閉じました");
            }
        }

        /// <summary>
        /// マウスボタンが押されたときの処理
        /// </summary>
        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_canvas == null || _selectionRectangle == null)
            {
                return;
            }

            var point = e.GetCurrentPoint(_canvas);
            _startX = point.Position.X;
            _startY = point.Position.Y;
            _isSelecting = true;

            // 矩形を表示
            _selectionRectangle.Visibility = Visibility.Visible;
            Canvas.SetLeft(_selectionRectangle, _startX);
            Canvas.SetTop(_selectionRectangle, _startY);
            _selectionRectangle.Width = 0;
            _selectionRectangle.Height = 0;

            e.Handled = true;
        }

        /// <summary>
        /// マウスが移動したときの処理
        /// </summary>
        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isSelecting || _canvas == null || _selectionRectangle == null)
            {
                return;
            }

            var point = e.GetCurrentPoint(_canvas);
            double currentX = point.Position.X;
            double currentY = point.Position.Y;

            // 矩形の位置とサイズを更新
            double left = Math.Min(_startX, currentX);
            double top = Math.Min(_startY, currentY);
            double width = Math.Abs(currentX - _startX);
            double height = Math.Abs(currentY - _startY);

            Canvas.SetLeft(_selectionRectangle, left);
            Canvas.SetTop(_selectionRectangle, top);
            _selectionRectangle.Width = width;
            _selectionRectangle.Height = height;

            e.Handled = true;
        }

        /// <summary>
        /// マウスボタンが離されたときの処理
        /// </summary>
        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isSelecting || _canvas == null || _selectionRectangle == null)
            {
                return;
            }

            var point = e.GetCurrentPoint(_canvas);
            double endX = point.Position.X;
            double endY = point.Position.Y;

            // 矩形の位置とサイズを計算
            int x = (int)Math.Min(_startX, endX);
            int y = (int)Math.Min(_startY, endY);
            int width = (int)Math.Abs(endX - _startX);
            int height = (int)Math.Abs(endY - _startY);

            // 最小サイズチェック
            if (width < 10 || height < 10)
            {
                Logger.Debug("選択領域が小さすぎます");
                CancelSelection();
                return;
            }

            Logger.Info($"矩形選択完了: x={x}, y={y}, width={width}, height={height}");

            // 選択ウィンドウを閉じる
            CloseSelection();

            // イベントを発火
            OnSelectionCompleted?.Invoke(x, y, width, height);

            e.Handled = true;
        }

        /// <summary>
        /// キーが押されたときの処理
        /// </summary>
        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            Logger.Info($"[Canvas]キー入力検出: Key={e.Key}, OriginalKey={e.OriginalKey}, KeyStatus={e.KeyStatus}");
            
            if (e.Key == VirtualKey.Escape)
            {
                Logger.Info("[Canvas]ESCキーが押されました。矩形選択をキャンセルします。");
                CancelSelection();
                e.Handled = true;
            }
            else
            {
                Logger.Debug($"[Canvas]ESC以外のキーが押されました: {e.Key}");
            }
        }

        /// <summary>
        /// キーが離されたときの処理（デバッグ用）
        /// </summary>
        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
            Logger.Debug($"[Canvas]キーが離されました: Key={e.Key}, OriginalKey={e.OriginalKey}");
        }

        /// <summary>
        /// マウスカーソルがキャンバスに入ったときの処理（十字カーソルに変更）
        /// CrosshairCanvasを使用しているため、ProtectedCursorが自動的に適用される
        /// </summary>
        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Logger.Debug("カーソルがキャンバスに入りました（十字カーソルが表示されます）");
        }

        /// <summary>
        /// マウスカーソルがキャンバスから出たときの処理（デフォルトカーソルに戻す）
        /// CrosshairCanvasを使用しているため、自動的にデフォルトカーソルに戻る
        /// </summary>
        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            Logger.Debug("カーソルがキャンバスから出ました（デフォルトカーソルに戻ります）");
        }

        /// <summary>
        /// ウィンドウプロシージャをフックしてWM_HOTKEYメッセージを処理
        /// </summary>
        private unsafe void HookWindowMessages(HWND hwnd)
        {
            try
            {
                // ウィンドウプロシージャをフック
                var originalWndProcPtr = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC);
                _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(originalWndProcPtr);
                _wndProcDelegate = new WNDPROC(WndProc);
                var newWndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
                PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, newWndProcPtr);
                
                Logger.Debug("矩形選択ウィンドウのメッセージフックを設定しました");
            }
            catch (Exception ex)
            {
                Logger.Warning($"メッセージフックの設定に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// ウィンドウメッセージフックを解除
        /// </summary>
        private unsafe void UnhookWindowMessages()
        {
            if (_windowHandle.HasValue && _originalWndProc != null)
            {
                try
                {
                    var originalWndProcPtr = Marshal.GetFunctionPointerForDelegate(_originalWndProc);
                    PInvoke.SetWindowLongPtr(_windowHandle.Value, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, originalWndProcPtr);
                    _originalWndProc = null;
                    _wndProcDelegate = null;
                    Logger.Debug("矩形選択ウィンドウのメッセージフックを解除しました");
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
        private unsafe LRESULT WndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        {
            const uint WM_HOTKEY = 0x0312;
            
            // WM_HOTKEYメッセージを処理
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = (int)wParam.Value;
                Logger.Info($"[WM_HOTKEY]ホットキーが押されました: ID={hotkeyId}");
                
                if (hotkeyId == ESC_HOTKEY_ID)
                {
                    Logger.Info("[WM_HOTKEY]ESCキーが押されました。矩形選択をキャンセルします。");
                    
                    // UIスレッドでキャンセル処理を実行
                    _selectionWindow?.DispatcherQueue.TryEnqueue(
                        Microsoft.UI.Dispatching.DispatcherQueuePriority.High,
                        () =>
                        {
                            CancelSelection();
                        });
                    
                    return new LRESULT(0);
                }
            }
            
            // デフォルトのウィンドウプロシージャを呼び出す
            if (_originalWndProc != null)
            {
                return _originalWndProc(hWnd, msg, wParam, lParam);
            }
            
            return new LRESULT(0);
        }

        public void Dispose()
        {
            // メッセージフックを解除
            UnhookWindowMessages();
            CloseSelection();
        }
    }
}

