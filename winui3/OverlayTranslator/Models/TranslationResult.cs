namespace OverlayTranslator.Models
{
    /// <summary>
    /// 翻訳結果を表すクラス
    /// </summary>
    public class TranslationResult
    {
        public string OriginalText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

