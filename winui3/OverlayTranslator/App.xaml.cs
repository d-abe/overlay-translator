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
using Windows.System;

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
        private Window? _aboutWindow;

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
                        _trayIconService.AboutClicked += OnAboutClicked;
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
        /// バージョン情報ボタンがクリックされたときの処理
        /// </summary>
        private async void OnAboutClicked(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("バージョン情報ボタンがクリックされました");
                
                // 既存のバージョン情報ウィンドウが開いている場合は前面に表示
                if (_aboutWindow != null)
                {
                    try
                    {
                        // ウィンドウが有効かどうかを確認（AppWindowがnullでないか）
                        if (_aboutWindow.AppWindow == null)
                        {
                            _aboutWindow = null;
                        }
                        else
                        {
                            // 既に開いているので、前面に表示
                            _aboutWindow.AppWindow.MoveInZOrderAtTop();
                            return;
                        }
                    }
                    catch
                    {
                        // ウィンドウが無効な場合は参照をクリア
                        _aboutWindow = null;
                    }
                }
                
                // 一時的なウィンドウを作成してContentDialogを表示
                var tempWindow = new Window();
                _aboutWindow = tempWindow;
                tempWindow.Content = new Microsoft.UI.Xaml.Controls.Grid();
                tempWindow.AppWindow.Title = "バージョン情報";
                tempWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(400, 464));
                
                // ウィンドウのアイコンを設定
                await SetAboutWindowIcon(tempWindow);
                
                // タイトルバーの色を設定（ダークモード/ライトモードに合わせる）
                var titleBar = tempWindow.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = false; // タイトルバーを通常表示
                
                // システムのテーマに合わせてタイトルバーの色を設定
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var systemBackgroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
                
                // ダークモードかどうかを判定（背景色が暗い場合）
                bool isDarkMode = systemBackgroundColor.R < 128;
                
                if (isDarkMode)
                {
                    // ダークモード: タイトルバーを暗く
                    titleBar.BackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32);
                    titleBar.ForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.InactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 20, 20, 20);
                    titleBar.InactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 200, 200, 200);
                    titleBar.ButtonBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32);
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 64, 64, 64);
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 48, 48, 48);
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 20, 20, 20);
                    titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 150, 150, 150);
                }
                else
                {
                    // ライトモード: タイトルバーを明るく
                    titleBar.BackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 243, 243, 243);
                    titleBar.ForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.InactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 243, 243, 243);
                    titleBar.InactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 100, 100, 100);
                    titleBar.ButtonBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 243, 243, 243);
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 230, 230, 230);
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220);
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 243, 243, 243);
                    titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 150, 150, 150);
                }
                
                // ウィンドウサイズ変更を監視してログに出力
                tempWindow.AppWindow.Changed += (sender, args) =>
                {
                    if (args.DidSizeChange)
                    {
                        var size = tempWindow.AppWindow.Size;
                        Logger.Info($"バージョン情報ウィンドウのサイズが変更されました: {size.Width}x{size.Height}");
                    }
                };
                
                // ウィンドウを中央に配置
                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
                    new Windows.Graphics.PointInt32(0, 0),
                    Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                if (displayArea != null)
                {
                    var centerX = (displayArea.WorkArea.Width - 400) / 2;
                    var centerY = (displayArea.WorkArea.Height - 464) / 2;
                    tempWindow.AppWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
                }
                
                var tcs = new TaskCompletionSource<bool>();
                var dialogShown = false;
                
                // ActivatedイベントでContentDialogを表示（一度だけ実行されるようにする）
                Windows.Foundation.TypedEventHandler<object, Microsoft.UI.Xaml.WindowActivatedEventArgs>? activatedHandler = null;
                activatedHandler = async (s, e) =>
                {
                    // 一度だけ実行されるようにする
                    if (dialogShown)
                    {
                        return;
                    }
                    dialogShown = true;
                    
                    // イベントハンドラーを解除
                    tempWindow.Activated -= activatedHandler;
                    
                    try
                    {
                        // 少し待ってからXamlRootを取得
                        await Task.Delay(100);
                        
                        // XamlRootを取得
                        var xamlRoot = tempWindow.Content?.XamlRoot;
                        if (xamlRoot == null)
                        {
                            Logger.Warning("XamlRootが取得できませんでした");
                            tempWindow.Close();
                            tcs.SetResult(false);
                            return;
                        }
                        
                        // バージョン情報ダイアログを表示
                        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                        {
                            Title = string.Empty, // タイトルを削除
                            Content = await CreateAboutContentAsync(),
                            CloseButtonText = "閉じる",
                            XamlRoot = xamlRoot
                        };
                        
                        await dialog.ShowAsync();
                        
                        // ダイアログを閉じた後、一時ウィンドウも閉じる
                        tempWindow.Close();
                        _aboutWindow = null; // 参照をクリア
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("ContentDialogの表示エラー", ex);
                        tempWindow.Close();
                        _aboutWindow = null; // 参照をクリア
                        tcs.SetResult(false);
                    }
                };
                
                tempWindow.Activated += activatedHandler;
                tempWindow.Activate();
                await tcs.Task;
            }
            catch (Exception ex)
            {
                Logger.Error("バージョン情報ダイアログの表示エラー", ex);
                _aboutWindow = null; // エラー時も参照をクリア
            }
        }

        /// <summary>
        /// バージョン情報ダイアログのコンテンツを作成
        /// </summary>
        private async Task<Microsoft.UI.Xaml.UIElement> CreateAboutContentAsync()
        {
            var stackPanel = new Microsoft.UI.Xaml.Controls.StackPanel
            {
                Spacing = 15,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
            };

            // アプリ名
            var appNameText = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = VersionInfo.GetAppName(),
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center
            };
            stackPanel.Children.Add(appNameText);

            // バージョン
            var versionText = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = $"バージョン {VersionInfo.GetVersion()}",
                FontSize = 14,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center
            };
            stackPanel.Children.Add(versionText);

            // Copyright
            var copyrightText = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = VersionInfo.GetCopyright(),
                FontSize = 12,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
            };
            stackPanel.Children.Add(copyrightText);

            // GitHubリンク（アイコン表示）
            // GitHubアイコンを画像として表示
            Microsoft.UI.Xaml.UIElement githubContent;
            
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "github-mark.png");
                if (File.Exists(iconPath))
                {
                    var storageFile = await StorageFile.GetFileFromPathAsync(iconPath);
                    var stream = await storageFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bitmapImage.SetSourceAsync(stream);
                    
                    var githubImage = new Microsoft.UI.Xaml.Controls.Image
                    {
                        Source = bitmapImage,
                        Width = 32,
                        Height = 32,
                        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                        VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
                        Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
                    };
                    githubContent = githubImage;
                }
                else
                {
                    // 画像が見つからない場合は、FontIconで代替表示
                    githubContent = new Microsoft.UI.Xaml.Controls.FontIcon
                    {
                        Glyph = "\uE71B", // リンクアイコン
                        FontSize = 32,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets")
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"GitHubアイコンの読み込みエラー: {ex.Message}");
                // エラー時は代替アイコン
                githubContent = new Microsoft.UI.Xaml.Controls.FontIcon
                {
                    Glyph = "\uE71B", // リンクアイコン
                    FontSize = 32,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets")
                };
            }
            
            var githubButton = new Microsoft.UI.Xaml.Controls.Button
            {
                Content = githubContent,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Microsoft.UI.Xaml.Thickness(0)
            };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(githubButton, "GitHub リポジトリを開く");
            githubButton.Click += (s, e) =>
            {
                var uri = new Uri(VersionInfo.GetGitHubUrl());
                _ = Launcher.LaunchUriAsync(uri);
            };
            stackPanel.Children.Add(githubButton);

            return stackPanel;
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

                        string finalText;

                        // 設定に応じてOCR処理を切り替え
                        if (_settings?.UseCombinedOCRTranslation ?? true)
                        {
                            // OCR+翻訳処理（同時実行モード）
                            Logger.Info("OCR+翻訳処理を開始します（同時実行モード）");
                            var ocrResult = await _ocrService.ExtractAndTranslateAsync(bitmap);
                            
                            if (!ocrResult.Success)
                            {
                                Logger.Error($"OCR+翻訳処理失敗: {ocrResult.ErrorMessage}");
                                return;
                            }

                            Logger.Info($"OCR処理成功: 元テキスト=\"{ocrResult.English}\", 翻訳テキスト=\"{ocrResult.Japanese}\"");

                            finalText = ocrResult.Japanese;

                            // 翻訳が失敗した場合（japaneseが空の場合）、別のモデルで再翻訳を試みる
                            if (string.IsNullOrWhiteSpace(ocrResult.Japanese) && !string.IsNullOrWhiteSpace(ocrResult.English))
                            {
                                Logger.Warning("OCR+翻訳モードで翻訳が取得できませんでした。TranslationServiceで再翻訳を試みます。");
                                
                                // 3. 翻訳処理（フォールバック）
                                if (_translationService == null)
                                {
                                    Logger.Warning("TranslationServiceが初期化されていません（APIキーが設定されていない可能性があります）");
                                    return;
                                }

                                Logger.Info("翻訳処理を開始します（フォールバック）");
                                var translationResult = await _translationService.TranslateToJapaneseAsync(ocrResult.English);

                                if (!translationResult.Success)
                                {
                                    Logger.Error($"翻訳処理失敗: {translationResult.ErrorMessage}");
                                    return;
                                }

                                Logger.Info($"翻訳処理成功: 翻訳テキスト=\"{translationResult.TranslatedText}\"");
                                finalText = translationResult.TranslatedText;
                            }
                            else if (string.IsNullOrWhiteSpace(ocrResult.English) && string.IsNullOrWhiteSpace(ocrResult.Japanese))
                            {
                                Logger.Warning("OCRでテキストが抽出されませんでした");
                                return;
                            }
                        }
                        else
                        {
                            // 通常モード: OCRと翻訳を別々に実行
                            Logger.Info("OCR処理を開始します（通常モード）");
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
                            finalText = translationResult.TranslatedText;
                        }

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
                            finalText, // 翻訳済みテキストを直接使用
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

        /// <summary>
        /// バージョン情報ウィンドウのアイコンを設定
        /// </summary>
        private async Task SetAboutWindowIcon(Window window)
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
                    
                    Logger.Info($"バージョン情報ウィンドウのアイコンを設定しました: {iconPath}");
                }
                else
                {
                    Logger.Warning($"アイコンファイルが見つかりません: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("バージョン情報ウィンドウのアイコン設定エラー", ex);
            }
        }
    }
}
