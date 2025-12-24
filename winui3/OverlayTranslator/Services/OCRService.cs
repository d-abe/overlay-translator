using System.Text;
using System.Text.Json;
using OverlayTranslator.Models;
using OverlayTranslator.Utils;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OverlayTranslator.Services
{
    /// <summary>
    /// OCR機能を提供するサービス
    /// 初期実装: Groq Vision APIのみ
    /// 将来的にWindows AI APIのTextRecognizerを追加可能
    /// </summary>
    public class OCRService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _visionModel;
        private const int MaxRetries = 3;
        private const int TimeoutSeconds = 30;

        public OCRService(string apiKey, string visionModel = "meta-llama/llama-4-scout-17b-16e-instruct")
        {
            _apiKey = apiKey;
            _visionModel = visionModel;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        }

        /// <summary>
        /// 画像からテキストを抽出（Groq Vision API）
        /// </summary>
        public async Task<string> ExtractTextAsync(SoftwareBitmap bitmap)
        {
            Exception? lastException = null;
            
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    Logger.Info($"OCR処理を開始します (試行 {retry + 1}/{MaxRetries})");
                    
                    // SoftwareBitmapをBase64エンコード
                    var base64Image = await ConvertBitmapToBase64Async(bitmap);
                    Logger.Debug($"画像をBase64エンコードしました (サイズ: {base64Image.Length} bytes)");

                    // Groq Vision APIにリクエスト
                    var requestBody = new
                    {
                        model = _visionModel,
                        messages = new object[]
                        {
                            new
                            {
                                role = "user",
                                content = new object[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = "この画像に含まれるすべてのテキストを正確に抽出してください。画像内の文字をそのまま、改行やスペースも含めて正確に出力してください。"
                                    },
                                    new
                                    {
                                        type = "image_url",
                                        image_url = new
                                        {
                                            url = $"data:image/png;base64,{base64Image}"
                                        }
                                    }
                                }
                            }
                        },
                        temperature = 0.1, // 低い温度でより正確なOCR
                        max_tokens = 2048
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
                        Logger.Warning($"OCR APIエラー: {response.StatusCode} - {errorContent}");
                        throw new HttpRequestException($"OCR APIエラー: {response.StatusCode}");
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

                    var text = responseObj.GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;

                    var extractedText = text.Trim();
                    Logger.Info($"OCR処理成功: 抽出テキスト長={extractedText.Length}");
                    return extractedText;
                }
                catch (TaskCanceledException ex)
                {
                    lastException = ex;
                    Logger.Warning($"OCR処理タイムアウト (試行 {retry + 1}/{MaxRetries}): {ex.Message}");
                    if (retry < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (retry + 1)); // 指数バックオフ
                        continue;
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    Logger.Warning($"OCR APIリクエストエラー (試行 {retry + 1}/{MaxRetries}): {ex.Message}");
                    if (retry < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (retry + 1)); // 指数バックオフ
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Logger.Error($"OCR処理エラー (試行 {retry + 1}/{MaxRetries})", ex);
                    if (retry < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (retry + 1)); // 指数バックオフ
                        continue;
                    }
                }
            }

            // すべてのリトライが失敗した場合
            Logger.Error("OCR処理がすべてのリトライに失敗しました", lastException);
            throw new Exception("OCR処理に失敗しました", lastException);
        }

        /// <summary>
        /// SoftwareBitmapをBase64エンコード
        /// </summary>
        private async Task<string> ConvertBitmapToBase64Async(SoftwareBitmap bitmap)
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();

            stream.Seek(0);
            var buffer = new byte[stream.Size];
            var dataReader = new DataReader(stream.GetInputStreamAt(0));
            await dataReader.LoadAsync((uint)stream.Size);
            dataReader.ReadBytes(buffer);

            return Convert.ToBase64String(buffer);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

