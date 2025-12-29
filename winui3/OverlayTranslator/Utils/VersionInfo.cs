using System.Reflection;

namespace OverlayTranslator.Utils
{
    /// <summary>
    /// アプリケーションのバージョン情報を取得するユーティリティ
    /// </summary>
    public static class VersionInfo
    {
        /// <summary>
        /// アプリケーションのバージョンを取得
        /// </summary>
        public static string GetVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    return $"{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"バージョン情報の取得エラー: {ex.Message}");
            }
            return "0.2.0";
        }

        /// <summary>
        /// アプリケーション名を取得
        /// </summary>
        public static string GetAppName()
        {
            return "Overlay Translator";
        }

        /// <summary>
        /// Copyright情報を取得
        /// </summary>
        public static string GetCopyright()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
                if (copyrightAttribute != null)
                {
                    return copyrightAttribute.Copyright;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Copyright情報の取得エラー: {ex.Message}");
            }
            return "Copyright © 2025 Daijiro Abe";
        }

        /// <summary>
        /// GitHubリポジトリのURLを取得
        /// </summary>
        public static string GetGitHubUrl()
        {
            return "https://github.com/d-abe/overlay-translator";
        }
    }
}

