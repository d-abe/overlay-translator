namespace OverlayTranslator.Models
{
    /// <summary>
    /// OCRと翻訳の結果を表すクラス
    /// </summary>
    public class OCRResult
    {
        /// <summary>
        /// 抽出された元のテキスト（英語など）
        /// </summary>
        public string English { get; set; } = string.Empty;

        /// <summary>
        /// 翻訳後の日本語テキスト
        /// </summary>
        public string Japanese { get; set; } = string.Empty;

        /// <summary>
        /// OCR処理が成功したかどうか
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// エラーメッセージ（失敗時）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}


