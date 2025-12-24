using OverlayTranslator.Utils;
using System;
using System.Runtime.InteropServices;
using System.Linq;

namespace OverlayTranslator.Services
{
    /// <summary>
    /// グローバルホットキー機能を提供するサービス
    /// Win32 APIのRegisterHotKeyを使用
    /// </summary>
    public class HotkeyService : IDisposable
    {
        // Win32 API定義
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern uint VkKeyScan(char ch);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const int WM_HOTKEY = 0x0312;

        private IntPtr _windowHandle;
        private int _hotkeyId = 1;
        private bool _isRegistered = false;
        private string _hotkey = "Ctrl+Shift+T";
        private uint _modifiers = 0;
        private uint _virtualKey = 0;

        public event EventHandler? HotkeyPressed;

        public HotkeyService(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            Logger.Debug("HotkeyServiceを初期化しました");
        }

        /// <summary>
        /// ホットキーを登録
        /// </summary>
        public bool RegisterHotkey(string hotkey)
        {
            try
            {
                if (_isRegistered)
                {
                    UnregisterHotkey();
                }

                _hotkey = hotkey;

                // ホットキー文字列を解析
                if (!ParseHotkey(hotkey, out uint modifiers, out uint virtualKey))
                {
                    Logger.Error($"ホットキーの解析に失敗しました: {hotkey}");
                    return false;
                }

                _modifiers = modifiers;
                _virtualKey = virtualKey;

                // ホットキーを登録
                bool result = RegisterHotKey(_windowHandle, _hotkeyId, modifiers, virtualKey);
                if (result)
                {
                    _isRegistered = true;
                    Logger.Info($"ホットキーを登録しました: {hotkey}");
                    return true;
                }
                else
                {
                    Logger.Error($"ホットキーの登録に失敗しました: {hotkey}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ホットキーの登録に失敗しました", ex);
                return false;
            }
        }

        /// <summary>
        /// ホットキー文字列を解析
        /// 例: "Ctrl+Shift+T" -> MOD_CONTROL | MOD_SHIFT, 'T'の仮想キーコード
        /// </summary>
        private bool ParseHotkey(string hotkey, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            try
            {
                var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                {
                    return false;
                }

                // 修飾キーを解析
                foreach (var part in parts.Take(parts.Length - 1))
                {
                    var partUpper = part.ToUpperInvariant();
                    switch (partUpper)
                    {
                        case "CTRL":
                        case "CONTROL":
                            modifiers |= MOD_CONTROL;
                            break;
                        case "SHIFT":
                            modifiers |= MOD_SHIFT;
                            break;
                        case "ALT":
                            modifiers |= MOD_ALT;
                            break;
                        case "WIN":
                        case "WINDOWS":
                            modifiers |= MOD_WIN;
                            break;
                    }
                }

                // 最後の部分がキー
                var keyPart = parts.Last();
                if (keyPart.Length == 1)
                {
                    // 単一文字（例: 'T'）
                    char keyChar = keyPart.ToUpperInvariant()[0];
                    uint vkScan = VkKeyScan(keyChar);
                    virtualKey = vkScan & 0xFF; // 下位8ビットが仮想キーコード
                }
                else
                {
                    // 特殊キー（例: "F1", "Enter"など）
                    // 簡易実装: 最初の文字のみを使用
                    if (keyPart.Length > 0)
                    {
                        char keyChar = keyPart.ToUpperInvariant()[0];
                        uint vkScan = VkKeyScan(keyChar);
                        virtualKey = vkScan & 0xFF;
                    }
                }

                return modifiers != 0 && virtualKey != 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"ホットキーの解析エラー: {hotkey}", ex);
                return false;
            }
        }

        /// <summary>
        /// ホットキーを解除
        /// </summary>
        public void UnregisterHotkey()
        {
            if (_isRegistered)
            {
                UnregisterHotKey(_windowHandle, _hotkeyId);
                _isRegistered = false;
                Logger.Info("ホットキーを解除しました");
            }
        }

        /// <summary>
        /// ホットキーメッセージを処理
        /// </summary>
        public void ProcessHotkeyMessage(int messageId)
        {
            if (messageId == _hotkeyId)
            {
                Logger.Debug("ホットキーが押されました");
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            UnregisterHotkey();
        }
    }
}
