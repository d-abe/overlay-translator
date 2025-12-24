using System.Text;
using System.Text.Json;
using OverlayTranslator.Models;
using OverlayTranslator.Utils;

namespace OverlayTranslator.Services
{
    /// <summary>
    /// 翻訳機能を提供するサービス（Groq API）
    /// </summary>
    public class TranslationService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private const int MaxRetries = 3;
        private const int TimeoutSeconds = 30;

        public TranslationService(string apiKey, string model = "llama-3.1-8b-instant")
        {
            _apiKey = apiKey;
            _model = model;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        }

        /// <summary>
        /// テキストを日本語に翻訳
        /// </summary>
        public async Task<TranslationResult> TranslateToJapaneseAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Logger.Warning("翻訳対象のテキストが空です");
                return new TranslationResult
                {
                    OriginalText = text,
                    TranslatedText = string.Empty,
                    Success = false,
                    ErrorMessage = "翻訳対象のテキストが空です"
                };
            }

            Exception? lastException = null;

            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    Logger.Info($"翻訳処理を開始します (試行 {retry + 1}/{MaxRetries}, テキスト長: {text.Length})");

                    var requestBody = new
                    {
                        model = _model,
                        messages = new[]
                        {
                            new
                            {
                                role = "system",
                                content = "あなたは優秀な翻訳者です。入力されたテキストを自然な日本語に翻訳してください。"
                            },
                            new
                            {
                                role = "user",
                                content = $"以下のテキストを日本語に翻訳してください:\n\n{text}"
                            }
                        },
                        temperature = 0.3,
                        max_tokens = 1000
                    };

                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        "https://api.groq.com/openai/v1/chat/completions",
                        content
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Logger.Warning($"翻訳APIエラー: {response.StatusCode} - {errorContent}");
                        throw new HttpRequestException($"翻訳APIエラー: {response.StatusCode}");
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

                    var translatedText = responseObj.GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;

                    var result = new TranslationResult
                    {
                        OriginalText = text,
                        TranslatedText = translatedText.Trim(),
                        Success = true
                    };

                    Logger.Info($"翻訳処理成功: 翻訳テキスト長={result.TranslatedText.Length}");
                    return result;
                }
                catch (TaskCanceledException ex)
                {
                    lastException = ex;
                    Logger.Warning($"翻訳処理タイムアウト (試行 {retry + 1}/{MaxRetries}): {ex.Message}");
                    if (retry < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (retry + 1)); // 指数バックオフ
                        continue;
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    Logger.Warning($"翻訳APIリクエストエラー (試行 {retry + 1}/{MaxRetries}): {ex.Message}");
                    if (retry < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (retry + 1)); // 指数バックオフ
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Logger.Error($"翻訳処理エラー (試行 {retry + 1}/{MaxRetries})", ex);
                    if (retry < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (retry + 1)); // 指数バックオフ
                        continue;
                    }
                }
            }

            // すべてのリトライが失敗した場合
            Logger.Error("翻訳処理がすべてのリトライに失敗しました", lastException);
            return new TranslationResult
            {
                OriginalText = text,
                TranslatedText = string.Empty,
                Success = false,
                ErrorMessage = lastException?.Message ?? "翻訳処理に失敗しました"
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

