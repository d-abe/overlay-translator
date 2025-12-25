namespace OverlayTranslator.Models
{
    /// <summary>
    /// アプリケーション設定を表すクラス
    /// </summary>
    public class Settings
    {
        public string GroqApiKey { get; set; } = string.Empty;
        public string GroqModel { get; set; } = "llama-3.1-8b-instant";
        public string GroqVisionModel { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";
        public string GoogleApiKey { get; set; } = string.Empty;
        public string OcrMethod { get; set; } = "groq"; // groq, windows-ai (将来)
        public string Hotkey { get; set; } = "Ctrl+Shift+T";
        public string OverlayFontFamily { get; set; } = "Meiryo";
        public int OverlayFontSize { get; set; } = 12;
        public string OverlayFontStyle { get; set; } = "normal";
        public bool UseCombinedOCRTranslation { get; set; } = true; // OCRと翻訳を同時に実行するか
        public bool DebugLogging { get; set; } = false;
    }
}

