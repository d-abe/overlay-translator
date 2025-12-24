using System;
using System.IO;
using Windows.Storage;

namespace OverlayTranslator.Utils
{
    /// <summary>
    /// ログ出力を管理するクラス
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new();
        private static string? _logFilePath;
        private static bool _debugLoggingEnabled = false;

        private static string GetLogFilePath()
        {
            if (_logFilePath == null)
            {
                try
                {
                    // パッケージ化されたアプリの場合
                    var localFolder = ApplicationData.Current.LocalFolder.Path;
                    _logFilePath = Path.Combine(localFolder, "translator.log");
                }
                catch (System.InvalidOperationException)
                {
                    // パッケージ化されていないアプリの場合、実行ディレクトリを使用
                    // InvalidOperationExceptionは正常な動作（無視してOK）
                    var appDirectory = AppContext.BaseDirectory;
                    _logFilePath = Path.Combine(appDirectory, "translator.log");
                }
                catch
                {
                    // その他のエラーの場合も、実行ディレクトリを使用
                    var appDirectory = AppContext.BaseDirectory;
                    _logFilePath = Path.Combine(appDirectory, "translator.log");
                }
            }
            return _logFilePath;
        }

        /// <summary>
        /// デバッグログの有効/無効を設定
        /// </summary>
        public static void SetDebugLogging(bool enabled)
        {
            _debugLoggingEnabled = enabled;
        }

        private static void WriteLog(string level, string message, Exception? exception = null)
        {
            // DEBUGログは設定が有効な場合のみ出力
            if (level == "DEBUG" && !_debugLoggingEnabled)
            {
                return;
            }

            lock (_lock)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var logMessage = $"{timestamp} - OverlayTranslator - {level} - {message}";
                    
                    if (exception != null)
                    {
                        logMessage += $"\n{exception}";
                    }

                    File.AppendAllText(GetLogFilePath(), logMessage + Environment.NewLine);
                    System.Diagnostics.Debug.WriteLine(logMessage); // Visual Studioの出力ウィンドウにも表示
                }
                catch
                {
                    // ログファイルへの書き込みに失敗した場合は無視
                }
            }
        }

        public static void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        public static void Error(string message, Exception? exception = null)
        {
            WriteLog("ERROR", message, exception);
        }
    }
}

