using Microsoft.UI.Xaml.Navigation;
using OverlayTranslator.Views;
using OverlayTranslator.Utils;
using OverlayTranslator.Models;
using OverlayTranslator.Services;
using Microsoft.UI.Windowing;
using Windows.Graphics.Imaging;
using Windows.Storage;
using System.IO;
using System;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace OverlayTranslator
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private Settings? _settings;
        private TrayIconService? _trayIconService;
        private HotkeyService? _hotkeyService;
        private WindowMessageHandler? _messageHandler;
        private SelectionService? _selectionService;
        private ScreenCaptureService? _screenCaptureService;
        private OCRService? _ocrService;
        private TranslationService? _translationService;
        private OverlayService? _overlayService;
        private SettingsWindow? _settingsWindow;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                // Loggerの初期化を試みる（失敗しても続行）
                try
                {
                    Logger.Info("アプリケーションを起動します");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ログ初期化エラー（無視）: {logEx.Message}");
                }

                // 設定を読み込む
                Settings? loadedSettings = null;
                try
                {
                    loadedSettings = await ConfigManager.LoadSettingsAsync();
                    try
                    {
                        Logger.Info($"設定を読み込みました (Hotkey: {loadedSettings.Hotkey}, DebugLogging: {loadedSettings.DebugLogging})");
                    }
                    catch { }
                }
                catch (Exception settingsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"設定読み込みエラー: {settingsEx.Message}");
                    // デフォルト設定を使用
                    loadedSettings = new Settings();
                }

                _settings = loadedSettings ?? new Settings();

                // ウィンドウを作成
                       window ??= new Window();

                       // オーバーレイアプリなので、メインウィンドウのコンテンツは不要
                       // 空のGridを設定して、Hello Worldなどのデフォルトコンテンツを表示しない
                       if (window.Content == null)
                       {
                           window.Content = new Microsoft.UI.Xaml.Controls.Grid();
                       }
                
                // メインウィンドウのアイコンを設定
                await SetWindowIcon(window);
                
                // ウィンドウハンドルを取得
                var windowHandle = WindowHelper.GetWindowHandle(window);
                if (windowHandle != IntPtr.Zero)
                {
                    try
                    {
                        // タスクトレイアイコンサービスを初期化
                        _trayIconService = new TrayIconService(windowHandle);
                        _trayIconService.SettingsClicked += OnSettingsClicked;
                        _trayIconService.QuitClicked += OnQuitClicked;
                        _trayIconService.Show();

                        // ホットキーサービスを初期化
                        _hotkeyService = new HotkeyService(windowHandle);
                        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
                        _hotkeyService.RegisterHotkey(_settings.Hotkey);

                        // メッセージハンドラーを初期化
                        _messageHandler = new WindowMessageHandler(window, _trayIconService, _hotkeyService);

                        // 矩形選択サービスを初期化
                        _selectionService = new SelectionService();
                        _selectionService.OnSelectionCompleted += OnSelectionCompleted;
                        _selectionService.OnSelectionCancelled += OnSelectionCancelled;

                               // 画面キャプチャサービスを初期化
                               _screenCaptureService = new ScreenCaptureService();

                               // OCRサービスを初期化
                               if (!string.IsNullOrEmpty(_settings.GroqApiKey))
                               {
                                   _ocrService = new OCRService(_settings.GroqApiKey, _settings.GroqVisionModel);
                               }

                               // 翻訳サービスを初期化
                               if (!string.IsNullOrEmpty(_settings.GroqApiKey))
                               {
                                   _translationService = new TranslationService(_settings.GroqApiKey, _settings.GroqModel);
                               }

                               // オーバーレイサービスを初期化
                               _overlayService = new OverlayService();

                               // OCRサービスを初期化
                               if (!string.IsNullOrEmpty(_settings.GroqApiKey))
                               {
                                   _ocrService = new OCRService(_settings.GroqApiKey, _settings.GroqVisionModel);
                               }

                               // 翻訳サービスを初期化
                               if (!string.IsNullOrEmpty(_settings.GroqApiKey))
                               {
                                   _translationService = new TranslationService(_settings.GroqApiKey, _settings.GroqModel);
                               }

                               // オーバーレイサービスを初期化
                               _overlayService = new OverlayService();

                               Logger.Info("サービスを初期化しました");
                    }
                    catch (Exception serviceEx)
                    {
                        Logger.Error("サービスの初期化エラー", serviceEx);
                    }
                }
                else
                {
                    Logger.Warning("ウィンドウハンドルが取得できませんでした");
                }

                // オーバーレイアプリなので、メインウィンドウは常に非表示（タスクトレイのみ）
                window.AppWindow.Hide();

                try
                {
                    Logger.Info("メインウィンドウを非表示にしました（タスクトレイのみ）");
                }
                catch { }
            }
            catch (Exception ex)
            {
                // ログ出力を試みる（失敗しても続行）
                try
                {
                    Logger.Error("アプリケーション起動エラー", ex);
                }
                catch { }
                
                // デバッグモードの場合は例外を再スロー
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    throw;
                }
                
                // リリースモードでは、エラーダイアログを表示
                System.Diagnostics.Debug.WriteLine($"アプリケーション起動エラー: {ex}");
            }
        }

        /// <summary>
        /// 現在の設定を取得
        /// </summary>
        public Settings GetSettings()
        {
            return _settings ?? new Settings();
        }

        /// <summary>
        /// 設定ボタンがクリックされたときの処理
        /// </summary>
        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("設定ボタンがクリックされました");
                
                // 既存の設定ウィンドウが開いている場合はスキップ
                if (_settingsWindow != null)
                {
                    // ウィンドウが既に閉じられている場合は参照をクリア
                    try
                    {
                        // ウィンドウが有効かどうかを確認（AppWindowがnullでないか）
                        if (_settingsWindow.AppWindow == null)
                        {
                            _settingsWindow = null;
                        }
                        else
                        {
                            // 既に開いているので、前面に表示
                            _settingsWindow.AppWindow.MoveInZOrderAtTop();
                            return;
                        }
                    }
                    catch
                    {
                        // ウィンドウが無効な場合は参照をクリア
                        _settingsWindow = null;
                    }
                }
                
                // 設定ウィンドウを表示
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, args) =>
                {
                    // ウィンドウが閉じられたときに参照をクリア
                    _settingsWindow = null;
                    
                    // 設定を再読み込みして反映
                    Task.Run(async () =>
                    {
                        try
                        {
                            var newSettings = await ConfigManager.LoadSettingsAsync();
                            
                            // UIスレッドで設定を更新
                            window?.DispatcherQueue.TryEnqueue(() =>
                            {
                                _settings = newSettings;
                                
                                // ホットキーを再登録
                                if (_hotkeyService != null && !string.IsNullOrEmpty(_settings.Hotkey))
                                {
                                    _hotkeyService.RegisterHotkey(_settings.Hotkey);
                                }
                                
                                // OCRサービスと翻訳サービスを再初期化（APIキーが変更された場合）
                                _ocrService?.Dispose();
                                _translationService?.Dispose();
                                
                                if (!string.IsNullOrEmpty(_settings.GroqApiKey))
                                {
                                    _ocrService = new OCRService(_settings.GroqApiKey, _settings.GroqVisionModel);
                                    _translationService = new TranslationService(_settings.GroqApiKey, _settings.GroqModel);
                                }
                                
                                Logger.Info("設定を再読み込みしました");
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("設定の再読み込みエラー", ex);
                        }
                    });
                };
                
                _settingsWindow.Activate();
            }
            catch (Exception ex)
            {
                Logger.Error("設定ボタンの処理エラー", ex);
            }
        }

        /// <summary>
        /// 終了ボタンがクリックされたときの処理
        /// </summary>
        private void OnQuitClicked(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("終了ボタンがクリックされました");
                
                // リソースを解放
                _trayIconService?.Dispose();
                _hotkeyService?.Dispose();
                _selectionService?.Dispose(); // IDisposableを実装済み
                _screenCaptureService?.Dispose(); // IDisposableを実装済み
                _ocrService?.Dispose();
                _translationService?.Dispose();
                _overlayService?.CloseOverlay(); // Disposeの代わり
                
                Exit();
            }
            catch (Exception ex)
            {
                Logger.Error("終了ボタンの処理エラー", ex);
            }
        }

        /// <summary>
        /// ホットキーが押されたときの処理
        /// </summary>
        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("ホットキーが押されました");
                _selectionService?.StartSelection();
            }
            catch (Exception ex)
            {
                Logger.Error("ホットキーの処理エラー", ex);
            }
        }

        /// <summary>
        /// 矩形選択が完了したときの処理
        /// </summary>
        private async void OnSelectionCompleted(int x, int y, int width, int height)
        {
            try
            {
                Logger.Info($"矩形選択完了: x={x}, y={y}, width={width}, height={height}");
                
                // 処理フロー: キャプチャ → OCR → 翻訳 → オーバーレイ表示
                try
                {
                    // 1. 画面キャプチャ
                    if (_screenCaptureService == null)
                    {
                        Logger.Error("ScreenCaptureServiceが初期化されていません");
                        return;
                    }

                    Logger.Info("画面キャプチャを開始します");
                    var bitmap = await _screenCaptureService.CaptureRegionAsync(x, y, width, height);
                    Logger.Info($"画面キャプチャ成功: サイズ={bitmap.PixelWidth}x{bitmap.PixelHeight}");

                    try
                    {
                        // 2. OCR処理
                        if (_ocrService == null)
                        {
                            Logger.Warning("OCRServiceが初期化されていません（APIキーが設定されていない可能性があります）");
                            return;
                        }

                        Logger.Info("OCR処理を開始します");
                        var extractedText = await _ocrService.ExtractTextAsync(bitmap);
                        Logger.Info($"OCR処理成功: 抽出テキスト=\"{extractedText}\"");

                        if (string.IsNullOrWhiteSpace(extractedText))
                        {
                            Logger.Warning("OCRでテキストが抽出されませんでした");
                            return;
                        }

                        // 3. 翻訳処理
                        if (_translationService == null)
                        {
                            Logger.Warning("TranslationServiceが初期化されていません（APIキーが設定されていない可能性があります）");
                            return;
                        }

                        Logger.Info("翻訳処理を開始します");
                        var translationResult = await _translationService.TranslateToJapaneseAsync(extractedText);

                        if (!translationResult.Success)
                        {
                            Logger.Error($"翻訳処理失敗: {translationResult.ErrorMessage}");
                            return;
                        }

                        Logger.Info($"翻訳処理成功: 翻訳テキスト=\"{translationResult.TranslatedText}\"");

                        // 4. オーバーレイ表示
                        if (_overlayService == null)
                        {
                            Logger.Error("OverlayServiceが初期化されていません");
                            return;
                        }

                        // 主要色を取得して背景色を決定
                        var dominantColor = _screenCaptureService.GetDominantColor(bitmap);
                        Logger.Info($"主要色: A={dominantColor.A}, R={dominantColor.R}, G={dominantColor.G}, B={dominantColor.B}");

                        // オーバーレイを表示（設定を渡す）
                        _overlayService.ShowOverlay(
                            x,
                            y,
                            width,
                            height,
                            translationResult.TranslatedText,
                            dominantColor,
                            _settings
                        );

                        Logger.Info("処理フローが完了しました");
                    }
                    finally
                    {
                        // ビットマップを解放
                        bitmap.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("処理フローエラー", ex);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("矩形選択完了の処理エラー", ex);
            }
        }

        /// <summary>
        /// 矩形選択がキャンセルされたときの処理
        /// </summary>
        private void OnSelectionCancelled()
        {
            try
            {
                Logger.Info("矩形選択がキャンセルされました");
            }
            catch (Exception ex)
            {
                Logger.Error("矩形選択キャンセルの処理エラー", ex);
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// ウィンドウアイコンを設定
        /// </summary>
        private async Task SetWindowIcon(Window window)
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
                    // ファイルからStorageFileを作成
                    var storageFile = await StorageFile.GetFileFromPathAsync(iconPath);
                    
                    // AppWindowにアイコンを設定
                    window.AppWindow.SetIcon(storageFile.Path);
                    
                    Logger.Info($"メインウィンドウのアイコンを設定しました: {iconPath}");
                }
                else
                {
                    Logger.Warning($"アイコンファイルが見つかりません: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("メインウィンドウのアイコン設定エラー", ex);
            }
        }
    }
}
