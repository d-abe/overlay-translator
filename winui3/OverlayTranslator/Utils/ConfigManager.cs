using System.Text.Json;
using Windows.Storage;
using OverlayTranslator.Models;

namespace OverlayTranslator.Utils
{
    /// <summary>
    /// 設定の保存・読み込みを管理するクラス
    /// パッケージ化されていないアプリでは、JSONファイルを使用
    /// </summary>
    public class ConfigManager
    {
        private const string SettingsKey = "AppSettings";
        private const string SettingsFileName = "appsettings.json";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// 設定ファイルのパスを取得
        /// </summary>
        private static string GetSettingsFilePath()
        {
            // パッケージ化されていないアプリでは、実行ディレクトリを使用
            var appDirectory = AppContext.BaseDirectory;
            return Path.Combine(appDirectory, SettingsFileName);
        }

        /// <summary>
        /// 設定を読み込む
        /// </summary>
        public static async Task<Settings> LoadSettingsAsync()
        {
            // 方法1: ApplicationData.Currentを試す（パッケージ化されたアプリ用）
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (localSettings.Values.TryGetValue(SettingsKey, out var settingsJson) && settingsJson is string json)
                {
                    var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
                    if (settings != null)
                    {
                        // デバッグログ設定を反映
                        Logger.SetDebugLogging(settings.DebugLogging);
                        Logger.Info("設定を読み込みました（ApplicationData）");
                        return settings;
                    }
                }
            }
            catch (System.InvalidOperationException)
            {
                // パッケージ化されていないアプリでは正常な動作（無視してOK）
                // デバッグログには出力しない（例外が表示されるため）
            }
            catch (Exception ex)
            {
                Logger.Debug($"ApplicationData.Currentが使用できません: {ex.Message}");
            }

                   // 方法2: JSONファイルから読み込む（パッケージ化されていないアプリ用）
                   try
                   {
                       var settingsPath = GetSettingsFilePath();
                       if (File.Exists(settingsPath))
                       {
                           var json = await File.ReadAllTextAsync(settingsPath);
                           var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
                           if (settings != null)
                           {
                               // デバッグログ設定を反映
                               Logger.SetDebugLogging(settings.DebugLogging);
                               Logger.Info("設定を読み込みました（JSONファイル）");
                               return settings;
                           }
                       }
                       else
                       {
                           // 設定ファイルが存在しない場合、デフォルト設定で作成
                           Logger.Info("設定ファイルが存在しません。デフォルト設定で作成します");
                           var defaultSettings = new Settings();
                           await SaveSettingsAsync(defaultSettings);
                           Logger.SetDebugLogging(defaultSettings.DebugLogging);
                           Logger.Info("デフォルト設定ファイルを作成しました");
                           return defaultSettings;
                       }
                   }
                   catch (Exception ex)
                   {
                       Logger.Warning($"設定ファイルの読み込みエラー: {ex.Message}");
                   }

                   Logger.Info("デフォルト設定を使用します");
                   var fallbackSettings = new Settings();
                   Logger.SetDebugLogging(fallbackSettings.DebugLogging);
                   return fallbackSettings;
        }

        /// <summary>
        /// 設定を保存する
        /// </summary>
        public static async Task SaveSettingsAsync(Settings settings)
        {
            // 方法1: ApplicationData.Currentを試す（パッケージ化されたアプリ用）
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                localSettings.Values[SettingsKey] = json;
                
                // デバッグログ設定を反映
                Logger.SetDebugLogging(settings.DebugLogging);
                Logger.Info("設定を保存しました（ApplicationData）");
                return;
            }
            catch (System.InvalidOperationException)
            {
                // パッケージ化されていないアプリでは正常な動作（無視してOK）
                // デバッグログには出力しない（例外が表示されるため）
            }
            catch (Exception ex)
            {
                // InvalidOperationExceptionの場合は無視（パッケージ化されていないアプリでは正常な動作）
                if (ex is not System.InvalidOperationException)
                {
                    Logger.Debug($"ApplicationData.Currentが使用できません: {ex.Message}");
                }
            }

            // 方法2: JSONファイルに保存（パッケージ化されていないアプリ用）
            try
            {
                var settingsPath = GetSettingsFilePath();
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                await File.WriteAllTextAsync(settingsPath, json);
                
                // デバッグログ設定を反映
                Logger.SetDebugLogging(settings.DebugLogging);
                Logger.Info("設定を保存しました（JSONファイル）");
            }
            catch (Exception ex)
            {
                Logger.Error("設定の保存エラー", ex);
                throw;
            }
        }
    }
}

