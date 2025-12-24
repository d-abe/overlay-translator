using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OverlayTranslator.Models;
using OverlayTranslator.Utils;
using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Foundation;
using WinRT.Interop;
using System.IO;
using Windows.Storage;

namespace OverlayTranslator.Views
{
    /// <summary>
    /// 設定ウィンドウ
    /// </summary>
    public sealed partial class SettingsWindow : Window
    {
        private Settings? _currentSettings;
        private string _selectedFontFamily = "Meiryo";
        private int _selectedFontSize = 12;
        private string _selectedFontStyle = "normal";

        /// <summary>
        /// CHOOSEFONTW構造体（Win32 API）
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private unsafe struct CHOOSEFONTW_STRUCT
        {
            public uint lStructSize;
            public HWND hwndOwner;
            public HDC hDC;
            public LOGFONTW* lpLogFont;
            public int iPointSize;
            public uint Flags;
            public uint rgbColors;
            public nint lCustData;
            public delegate* unmanaged<HWND, uint, WPARAM, LPARAM, nint> lpfnHook;
            public PCWSTR lpTemplateName;
            public HINSTANCE hInstance;
            public PCWSTR lpszStyle;
            public ushort nFontType;
            public ushort ___MISSING_ALIGNMENT__;
            public int nSizeMin;
            public int nSizeMax;
        }

        /// <summary>
        /// ChooseFontW関数（Win32 API - comdlg32.dll）
        /// </summary>
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern unsafe bool ChooseFontW(CHOOSEFONTW_STRUCT* lpcf);

        /// <summary>
        /// MulDiv関数（Win32 API）
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern int MulDiv(int nNumber, int nNumerator, int nDenominator);

        public SettingsWindow()
        {
            this.InitializeComponent();
            
            // ウィンドウのタイトルとサイズを設定
            this.AppWindow.Title = "設定 - Overlay Translator";
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(650, 985));
            this.AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
            
            // タイトルバーの色を設定（ダークモード/ライトモードに合わせる）
            var titleBar = this.AppWindow.TitleBar;
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
                titleBar.BackgroundColor = Microsoft.UI.Colors.White;
                titleBar.ForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.InactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 240, 240, 240);
                titleBar.InactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 100, 100, 100);
                titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 240, 240, 240);
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220);
                titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 240, 240, 240);
                titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 150, 150, 150);
            }
            
            // ウィンドウを中央に配置
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                this.AppWindow.Id, 
                Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var centerX = (displayArea.WorkArea.Width - 650) / 2;
                var centerY = (displayArea.WorkArea.Height - 985) / 2;
                this.AppWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
            }
            
            // アイコンを設定
            SetWindowIcon();
            
            // 設定を読み込む
            LoadSettingsAsync();
            
            // 変更を監視
            SetupChangeHandlers();
        }

        /// <summary>
        /// 設定を読み込む
        /// </summary>
        private async void LoadSettingsAsync()
        {
            try
            {
                _currentSettings = await ConfigManager.LoadSettingsAsync();
                ApplySettingsToUI(_currentSettings);
                Logger.Info("設定を読み込みました");
            }
            catch (Exception ex)
            {
                Logger.Error("設定の読み込みエラー", ex);
                // デフォルト設定を使用
                _currentSettings = new Settings();
                ApplySettingsToUI(_currentSettings);
            }
        }

        /// <summary>
        /// 設定をUIに適用
        /// </summary>
        private void ApplySettingsToUI(Settings settings)
        {
            GroqApiKeyBox.Password = settings.GroqApiKey ?? string.Empty;
            
            // モデルを選択（説明部分を除去して比較）
            foreach (ComboBoxItem item in GroqModelComboBox.Items)
            {
                string itemContent = item.Content?.ToString() ?? "";
                string modelName = ExtractModelName(itemContent);
                if (modelName == settings.GroqModel)
                {
                    GroqModelComboBox.SelectedItem = item;
                    break;
                }
            }
            if (GroqModelComboBox.SelectedItem == null && GroqModelComboBox.Items.Count > 0)
            {
                GroqModelComboBox.SelectedIndex = 0;
            }
            
            // Visionモデルを選択（説明部分を除去して比較）
            foreach (ComboBoxItem item in GroqVisionModelComboBox.Items)
            {
                string itemContent = item.Content?.ToString() ?? "";
                string modelName = ExtractModelName(itemContent);
                if (modelName == settings.GroqVisionModel)
                {
                    GroqVisionModelComboBox.SelectedItem = item;
                    break;
                }
            }
            if (GroqVisionModelComboBox.SelectedItem == null && GroqVisionModelComboBox.Items.Count > 0)
            {
                GroqVisionModelComboBox.SelectedIndex = 0;
            }
            
            HotkeyTextBox.Text = settings.Hotkey ?? "Ctrl+Shift+T";
            
            // フォント情報を保存して表示
            _selectedFontFamily = settings.OverlayFontFamily ?? "Meiryo";
            _selectedFontSize = settings.OverlayFontSize > 0 ? settings.OverlayFontSize : 12;
            _selectedFontStyle = settings.OverlayFontStyle ?? "normal";
            UpdateFontDisplay(_selectedFontFamily, _selectedFontSize, _selectedFontStyle);
            
            DebugLoggingCheckBox.IsChecked = settings.DebugLogging;
        }

        /// <summary>
        /// UIから設定を取得
        /// </summary>
        private Settings GetSettingsFromUI()
        {
            return new Settings
            {
                GroqApiKey = GroqApiKeyBox.Password ?? string.Empty,
                GroqModel = ExtractModelName((GroqModelComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "llama-3.1-8b-instant"),
                GroqVisionModel = ExtractModelName((GroqVisionModelComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "meta-llama/llama-4-scout-17b-16e-instruct"),
                Hotkey = HotkeyTextBox.Text ?? "Ctrl+Shift+T",
                OverlayFontFamily = _selectedFontFamily,
                OverlayFontSize = _selectedFontSize,
                OverlayFontStyle = _selectedFontStyle,
                DebugLogging = DebugLoggingCheckBox.IsChecked ?? false
            };
        }

        /// <summary>
        /// 変更監視を設定
        /// </summary>
        private void SetupChangeHandlers()
        {
            // 変更監視（必要に応じて実装）
            // 現時点では、保存時にすべての値を取得するため、個別の変更監視は不要
            // ただし、ComboBoxの選択変更イベントは自動的に処理される
        }

        /// <summary>
        /// 保存ボタンがクリックされたときの処理
        /// </summary>
        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = GetSettingsFromUI();
                
                // 設定を保存
                await ConfigManager.SaveSettingsAsync(settings);
                
                Logger.Info("設定を保存しました");
                
                // ウィンドウを閉じる
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.Error("設定の保存エラー", ex);
                
                // エラーダイアログを表示
                var dialog = new ContentDialog
                {
                    Title = "エラー",
                    Content = $"設定の保存に失敗しました:\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        /// <summary>
        /// キャンセルボタンがクリックされたときの処理
        /// </summary>
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// フォント選択ボタンがクリックされたときの処理
        /// Win32 APIのChooseFontダイアログを使用
        /// </summary>
        private unsafe void OnSelectFontClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // 現在のフォント設定からLOGFONT構造体を作成
                LOGFONTW logFont = new LOGFONTW();
                
                // フォント名を設定
                string fontName = _selectedFontFamily;
                if (fontName.Length > 31)
                {
                    fontName = fontName.Substring(0, 31);
                }
                for (int i = 0; i < fontName.Length && i < 32; i++)
                {
                    logFont.lfFaceName[i] = fontName[i];
                }
                
                // フォントサイズを設定（ポイントサイズから論理単位に変換）
                HDC hdc = PInvoke.GetDC(HWND.Null);
                int dpi = PInvoke.GetDeviceCaps(hdc, GET_DEVICE_CAPS_INDEX.LOGPIXELSX);
                PInvoke.ReleaseDC(HWND.Null, hdc);
                logFont.lfHeight = -MulDiv(_selectedFontSize, dpi, 72);
                
                // フォントスタイルを設定
                if (_selectedFontStyle.Contains("bold", StringComparison.OrdinalIgnoreCase))
                {
                    logFont.lfWeight = 700; // FW_BOLD
                }
                else
                {
                    logFont.lfWeight = 400; // FW_NORMAL
                }
                
                if (_selectedFontStyle.Contains("italic", StringComparison.OrdinalIgnoreCase))
                {
                    logFont.lfItalic = 1;
                }
                else
                {
                    logFont.lfItalic = 0;
                }
                
                // CHOOSEFONT構造体を作成
                CHOOSEFONTW_STRUCT cf = new CHOOSEFONTW_STRUCT();
                cf.lStructSize = (uint)Marshal.SizeOf<CHOOSEFONTW_STRUCT>();
                cf.hwndOwner = new HWND(WindowHelper.GetWindowHandle(this));
                cf.lpLogFont = &logFont;
                cf.Flags = 0x00000040 | 0x00000001 | 0x00000100; // CF_INITTOLOGFONTSTRUCT | CF_SCREENFONTS | CF_EFFECTS
                cf.rgbColors = 0; // デフォルトのテキスト色
                cf.nFontType = 0;
                
                // フォント選択ダイアログを表示（comdlg32.dllのChooseFontW）
                bool result = ChooseFontW(&cf);
                
                if (result)
                {
                    // 選択されたフォント情報を取得
                    // lfFaceNameは__char_32型なので、構造体のアドレスからオフセットを計算
                    string selectedFontName;
                    unsafe
                    {
                        // lfFaceNameのオフセットを計算（LOGFONTW構造体のメンバー順序に基づく）
                        nint offset = Marshal.OffsetOf<LOGFONTW>(nameof(LOGFONTW.lfFaceName));
                        char* faceNamePtr = (char*)((byte*)&logFont + offset);
                        selectedFontName = new string(faceNamePtr);
                    }
                    
                    int selectedSize = -MulDiv((int)logFont.lfHeight, 72, dpi);
                    if (selectedSize < 0) selectedSize = -selectedSize;
                    
                    string selectedStyle = "normal";
                    if (logFont.lfWeight >= 700)
                    {
                        selectedStyle = logFont.lfItalic != 0 ? "bold italic" : "bold";
                    }
                    else if (logFont.lfItalic != 0)
                    {
                        selectedStyle = "italic";
                    }
                    
                    // 選択されたフォント情報を保存
                    _selectedFontFamily = selectedFontName;
                    _selectedFontSize = selectedSize;
                    _selectedFontStyle = selectedStyle;
                    
                    // 表示を更新
                    UpdateFontDisplay(_selectedFontFamily, _selectedFontSize, _selectedFontStyle);
                    
                    Logger.Info($"フォントを選択しました: {_selectedFontFamily}, {_selectedFontSize}pt, {_selectedFontStyle}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("フォント選択ダイアログの表示エラー", ex);
                
                // エラーダイアログを表示
                var dialog = new ContentDialog
                {
                    Title = "エラー",
                    Content = $"フォント選択ダイアログの表示に失敗しました:\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }

        /// <summary>
        /// フォント表示を更新
        /// </summary>
        private void UpdateFontDisplay(string fontFamily, int fontSize, string fontStyle)
        {
            string styleDisplay = fontStyle switch
            {
                "bold" => "太字",
                "italic" => "斜体",
                "bold italic" => "太字・斜体",
                _ => "標準"
            };
            
            FontDisplayTextBox.Text = $"{fontFamily}, {styleDisplay}, {fontSize}pt";
        }

        /// <summary>
        /// モデル名から説明部分を除去して抽出
        /// 例: "llama-3.1-8b-instant（高速・低コスト・推奨）" -> "llama-3.1-8b-instant"
        /// </summary>
        private string ExtractModelName(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }
            
            // 「（」が含まれている場合は、その前の部分をモデル名として返す
            int index = content.IndexOf('（');
            if (index >= 0)
            {
                return content.Substring(0, index).Trim();
            }
            
            return content.Trim();
        }

        /// <summary>
        /// ウィンドウアイコンを設定
        /// </summary>
        private async void SetWindowIcon()
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
                    this.AppWindow.SetIcon(storageFile.Path);
                    
                    Logger.Info($"設定ウィンドウのアイコンを設定しました: {iconPath}");
                }
                else
                {
                    Logger.Warning($"アイコンファイルが見つかりません: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("設定ウィンドウのアイコン設定エラー", ex);
            }
        }
    }
}

