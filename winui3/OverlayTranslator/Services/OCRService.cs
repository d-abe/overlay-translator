using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
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
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        public OCRService(string apiKey, string visionModel = "meta-llama/llama-4-scout-17b-16e-instruct")
        {
            _apiKey = apiKey;
            _visionModel = visionModel;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        }

        /// <summary>
        /// 画像からテキストを抽出し、同時に日本語に翻訳（Groq Vision API）
        /// JSON形式で {"english": "...", "japanese": "..."} を返す
        /// </summary>
        public async Task<OCRResult> ExtractAndTranslateAsync(SoftwareBitmap bitmap)
        {
            Exception? lastException = null;
            
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    Logger.Info($"OCR+翻訳処理を開始します (試行 {retry + 1}/{MaxRetries})");
                    
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
                                        text = "この画像に含まれるすべてのテキストを正確に抽出し、それを自然な日本語に翻訳してください。結果は必ず以下のJSON形式のみで返してください。説明文やその他のテキストは一切含めないでください:\n{\"english\": \"抽出された元のテキスト\", \"japanese\": \"翻訳後の日本語テキスト\"}"
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

                    var json = JsonSerializer.Serialize(requestBody, JsonOptions);
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
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson, JsonOptions);

                    var text = responseObj.GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;

                    var extractedText = text.Trim();
                    Logger.Debug($"OCR+翻訳APIレスポンス（生）: {extractedText}");

                    // JSON部分を抽出
                    string jsonText = ExtractJsonFromResponse(extractedText);
                    Logger.Debug($"抽出されたJSON: {jsonText}");

                    // JSON形式のレスポンスをパース
                    try
                    {
                        // レスポンスがJSON形式かどうかを確認
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonText, JsonOptions);
                        var english = jsonElement.TryGetProperty("english", out var englishProp) 
                            ? englishProp.GetString() ?? string.Empty 
                            : string.Empty;
                        var japanese = jsonElement.TryGetProperty("japanese", out var japaneseProp) 
                            ? japaneseProp.GetString() ?? string.Empty 
                            : string.Empty;

                        Logger.Info($"OCR+翻訳処理成功: 元テキスト長={english.Length}, 翻訳テキスト長={japanese.Length}");
                        return new OCRResult
                        {
                            English = english,
                            Japanese = japanese,
                            Success = true
                        };
                    }
                    catch (JsonException ex)
                    {
                        // JSONパースに失敗した場合、破損したJSONからでも japanese フィールドを抽出を試みる
                        Logger.Warning($"JSON形式のパースに失敗しました。破損したJSONから japanese フィールドを抽出を試みます: {ex.Message}");
                        
                        // 正規表現で "japanese" フィールドを抽出
                        var japaneseMatch = ExtractJapaneseFromBrokenJson(jsonText);
                        if (japaneseMatch != null)
                        {
                            // japanese フィールドが見つかった場合
                            var englishMatch = ExtractEnglishFromBrokenJson(jsonText);
                            Logger.Info($"破損したJSONから抽出成功: 元テキスト長={englishMatch?.Length ?? 0}, 翻訳テキスト長={japaneseMatch.Length}");
                            return new OCRResult
                            {
                                English = englishMatch ?? string.Empty,
                                Japanese = japaneseMatch,
                                Success = true
                            };
                        }
                        else
                        {
                            // japanese フィールドが見つからない場合、リトライを試みる
                            Logger.Warning("破損したJSONから japanese フィールドを抽出できませんでした。リトライを試みます。");
                            throw; // 例外を再スローしてリトライ処理に委ねる
                        }
                    }
                }
                catch (TaskCanceledException ex)
                {
                    lastException = ex;
                    Logger.Warning($"OCR+翻訳処理タイムアウト (試行 {retry + 1}/{MaxRetries}): {ex.Message}");
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
                    Logger.Error($"OCR+翻訳処理エラー (試行 {retry + 1}/{MaxRetries})", ex);
                    if (retry < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (retry + 1)); // 指数バックオフ
                        continue;
                    }
                }
            }

            // すべてのリトライが失敗した場合
            Logger.Error("OCR+翻訳処理がすべてのリトライに失敗しました", lastException);
            return new OCRResult
            {
                Success = false,
                ErrorMessage = lastException?.Message ?? "OCR+翻訳処理に失敗しました"
            };
        }

        /// <summary>
        /// レスポンスからJSON部分を抽出
        /// 1. ```json と ``` の間を抽出
        /// 2. それが失敗したら、最初の { から最後の } までを抽出
        /// </summary>
        private string ExtractJsonFromResponse(string response)
        {
            // 1. ```json と ``` の間を抽出
            var codeBlockPattern = @"```json\s*(\{.*?\})\s*```";
            var codeBlockMatch = Regex.Match(response, codeBlockPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (codeBlockMatch.Success)
            {
                return codeBlockMatch.Groups[1].Value.Trim();
            }

            // 2. ``` と ``` の間を抽出（json指定なしの場合）
            var genericCodeBlockPattern = @"```\s*(\{.*?\})\s*```";
            var genericCodeBlockMatch = Regex.Match(response, genericCodeBlockPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (genericCodeBlockMatch.Success)
            {
                return genericCodeBlockMatch.Groups[1].Value.Trim();
            }

            // 3. 最初の { から最後の } までを抽出（ネストされたJSONに対応）
            int firstBrace = response.IndexOf('{');
            if (firstBrace >= 0)
            {
                int braceCount = 0;
                int lastBrace = -1;
                for (int i = firstBrace; i < response.Length; i++)
                {
                    if (response[i] == '{')
                    {
                        braceCount++;
                    }
                    else if (response[i] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            lastBrace = i;
                            break;
                        }
                    }
                }

                if (lastBrace > firstBrace)
                {
                    return response.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                }
            }

            // 4. 抽出に失敗した場合は元のレスポンスを返す
            Logger.Warning("JSON部分の抽出に失敗しました。元のレスポンスを返します。");
            return response;
        }

        /// <summary>
        /// 破損したJSONから "japanese" フィールドの値を抽出
        /// </summary>
        private string? ExtractJapaneseFromBrokenJson(string jsonText)
        {
            try
            {
                // "japanese": "..." のパターンを検索
                // 最後の " が存在しない場合（JSONが破損している場合）にも対応
                // パターン1: 通常の形式 "japanese": "..."
                var pattern1 = @"""japanese""\s*:\s*""((?:[^""\\]|\\.)*)""";
                var match1 = Regex.Match(jsonText, pattern1, RegexOptions.IgnoreCase);
                
                if (match1.Success && match1.Groups.Count > 1)
                {
                    var japaneseValue = match1.Groups[1].Value;
                    // エスケープシーケンスを解除
                    japaneseValue = japaneseValue.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
                    Logger.Debug($"破損したJSONから japanese フィールドを抽出しました（通常形式）: 長さ={japaneseValue.Length}");
                    return japaneseValue;
                }
                
                // パターン2: 最後の " が存在しない形式 "japanese": "...（終端まで）
                var pattern2 = @"""japanese""\s*:\s*""((?:[^""\\]|\\.)*?)(?:""|$|\s*[,}])";
                var match2 = Regex.Match(jsonText, pattern2, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                
                if (match2.Success && match2.Groups.Count > 1)
                {
                    var japaneseValue = match2.Groups[1].Value;
                    // エスケープシーケンスを解除
                    japaneseValue = japaneseValue.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
                    Logger.Debug($"破損したJSONから japanese フィールドを抽出しました（破損形式）: 長さ={japaneseValue.Length}");
                    return japaneseValue;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"japanese フィールドの抽出エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 破損したJSONから "english" フィールドの値を抽出
        /// </summary>
        private string? ExtractEnglishFromBrokenJson(string jsonText)
        {
            try
            {
                // "english": "..." のパターンを検索
                // 最後の " が存在しない場合（JSONが破損している場合）にも対応
                // パターン1: 通常の形式 "english": "..."
                var pattern1 = @"""english""\s*:\s*""((?:[^""\\]|\\.)*)""";
                var match1 = Regex.Match(jsonText, pattern1, RegexOptions.IgnoreCase);
                
                if (match1.Success && match1.Groups.Count > 1)
                {
                    var englishValue = match1.Groups[1].Value;
                    // エスケープシーケンスを解除
                    englishValue = englishValue.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
                    Logger.Debug($"破損したJSONから english フィールドを抽出しました（通常形式）: 長さ={englishValue.Length}");
                    return englishValue;
                }
                
                // パターン2: 最後の " が存在しない形式 "english": "...（終端まで）
                // "english": " の後に続く文字列を、次の " または行末まで抽出
                var pattern2 = @"""english""\s*:\s*""((?:[^""\\]|\\.)*?)(?:""|$)";
                var match2 = Regex.Match(jsonText, pattern2, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                
                if (match2.Success && match2.Groups.Count > 1)
                {
                    var englishValue = match2.Groups[1].Value;
                    // エスケープシーケンスを解除
                    englishValue = englishValue.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
                    Logger.Debug($"破損したJSONから english フィールドを抽出しました（破損形式）: 長さ={englishValue.Length}");
                    return englishValue;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"english フィールドの抽出エラー: {ex.Message}");
                return null;
            }
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
                                        // 一時的なテスト: OCRと翻訳を同時に実行
                                        text = "この画像に含まれるすべてのテキストを正確に抽出し、それを自然な日本語に翻訳してください。翻訳結果のみを出力してください。"
                                        // 元のプロンプト（後で戻す）:
                                        // text = "この画像に含まれるすべてのテキストを正確に抽出してください。画像内の文字をそのまま、改行やスペースも含めて正確に出力してください。"
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

                    var json = JsonSerializer.Serialize(requestBody, JsonOptions);
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
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson, JsonOptions);

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

