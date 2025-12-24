using OverlayTranslator.Utils;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml.Media.Imaging;

// IMemoryBufferByteAccessインターフェースの定義
[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}

namespace OverlayTranslator.Services
{
    /// <summary>
    /// 画面キャプチャ機能を提供するサービス
    /// Win32 APIのBitBltを使用して指定領域をキャプチャ
    /// </summary>
    public class ScreenCaptureService : IDisposable
    {
        /// <summary>
        /// DPIスケーリングファクターを取得
        /// Win32 APIのGetDeviceCapsを使用してプライマリモニターのDPIを取得
        /// </summary>
        private static double GetDpiScaleFactor()
        {
            try
            {
                // プライマリモニターのデバイスコンテキストを取得
                HDC hdc = PInvoke.GetDC(HWND.Null);
                try
                {
                    // LOGPIXELSX = 88 (GetDeviceCapsの定数)
                    const int LOGPIXELSX = 88;
                    // GET_DEVICE_CAPS_INDEXが生成されていない場合は、intとして直接使用
                    int dpiX = PInvoke.GetDeviceCaps(hdc, (GET_DEVICE_CAPS_INDEX)LOGPIXELSX);
                    
                    // 標準DPI (96) に対するスケーリングファクターを計算
                    double scale = dpiX / 96.0;
                    Logger.Info($"GetDeviceCapsから取得したDPI: {dpiX}, スケール: {scale}");
                    return scale;
                }
                finally
                {
                    PInvoke.ReleaseDC(HWND.Null, hdc);
                }
            }
            catch (Exception ex)
            {
                // エラー時は1.0を返す（スケーリングなし）
                Logger.Warning($"DPIスケールの取得に失敗しました: {ex.Message}。デフォルト値1.0を使用します。");
                return 1.0;
            }
        }
        // 最後にキャプチャしたbitmapDataを保持（主要色計算用）
        private byte[]? _lastCapturedBitmapData = null;
        private int _lastCapturedWidth = 0;
        private int _lastCapturedHeight = 0;

        /// <summary>
        /// 指定された領域をキャプチャ
        /// </summary>
        public async Task<SoftwareBitmap> CaptureRegionAsync(int x, int y, int width, int height)
        {
            try
            {
                Logger.Debug($"画面キャプチャ開始: x={x}, y={y}, width={width}, height={height}");
                
                // DPIスケーリングファクターを取得
                double dpiScale = GetDpiScaleFactor();
                Logger.Info($"DPIスケーリングファクター: {dpiScale}");
                
                // 論理座標を物理座標に変換
                int physicalX = (int)(x * dpiScale);
                int physicalY = (int)(y * dpiScale);
                int physicalWidth = (int)(width * dpiScale);
                int physicalHeight = (int)(height * dpiScale);
                
                Logger.Info($"論理座標: x={x}, y={y}, width={width}, height={height}");
                Logger.Info($"物理座標: x={physicalX}, y={physicalY}, width={physicalWidth}, height={physicalHeight}");

                // 画面のデバイスコンテキストを取得
                HDC hdcScreen = PInvoke.GetDC(HWND.Null);
                unsafe
                {
                    if ((IntPtr)hdcScreen.Value == IntPtr.Zero)
                    {
                        throw new Exception("画面のデバイスコンテキストの取得に失敗しました");
                    }
                }

                try
                {
                    // メモリデバイスコンテキストを作成
                    HDC hdcMem = PInvoke.CreateCompatibleDC(hdcScreen);
                    unsafe
                    {
                        if ((IntPtr)hdcMem.Value == IntPtr.Zero)
                        {
                            throw new Exception("メモリデバイスコンテキストの作成に失敗しました");
                        }
                    }

                    try
                    {
                        // ビットマップを作成（物理サイズで作成）
                        HBITMAP hBitmap = PInvoke.CreateCompatibleBitmap(hdcScreen, physicalWidth, physicalHeight);
                        unsafe
                        {
                            if ((IntPtr)hBitmap.Value == IntPtr.Zero)
                            {
                                throw new Exception("ビットマップの作成に失敗しました");
                            }
                        }

                        try
                        {
                            // ビットマップをメモリデバイスコンテキストに選択
                            HGDIOBJ hOldBitmap = PInvoke.SelectObject(hdcMem, hBitmap);

                            try
                            {
                                // 画面からメモリにコピー（物理座標を使用）
                                bool success = PInvoke.BitBlt(
                                    hdcMem,
                                    0,
                                    0,
                                    physicalWidth,
                                    physicalHeight,
                                    hdcScreen,
                                    physicalX,
                                    physicalY,
                                    ROP_CODE.SRCCOPY
                                );

                                if (!success)
                                {
                                    throw new Exception($"BitBltに失敗しました (エラーコード: {Marshal.GetLastWin32Error()})");
                                }

                                // ビットマップデータを取得
                                var bitmapData = GetBitmapData(hBitmap, physicalWidth, physicalHeight);

                                // 主要色計算用にbitmapDataを保持
                                _lastCapturedBitmapData = bitmapData;
                                _lastCapturedWidth = physicalWidth;
                                _lastCapturedHeight = physicalHeight;

                                // SoftwareBitmapに変換（物理サイズのまま）
                                var softwareBitmap = await ConvertToSoftwareBitmapAsync(bitmapData, physicalWidth, physicalHeight);

                                Logger.Info($"画面キャプチャ完了: 物理サイズ={physicalWidth}x{physicalHeight}, 論理サイズ={width}x{height}, DPIスケール={dpiScale}");
                                return softwareBitmap;
                            }
                            finally
                            {
                                // ビットマップの選択を解除
                                PInvoke.SelectObject(hdcMem, hOldBitmap);
                            }
                        }
                        finally
                        {
                            // ビットマップを削除
                            PInvoke.DeleteObject(hBitmap);
                        }
                    }
                    finally
                    {
                        // メモリデバイスコンテキストを削除
                        PInvoke.DeleteDC(hdcMem);
                    }
                }
                finally
                {
                    // 画面のデバイスコンテキストを解放
                    PInvoke.ReleaseDC(HWND.Null, hdcScreen);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("画面キャプチャエラー", ex);
                throw;
            }
        }

        /// <summary>
        /// ビットマップデータを取得
        /// </summary>
        private unsafe byte[] GetBitmapData(HBITMAP hBitmap, int width, int height)
        {
            // BITMAPINFO構造体を作成
            BITMAPINFO bmpInfo = new BITMAPINFO();
            bmpInfo.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            bmpInfo.bmiHeader.biWidth = width;
            bmpInfo.bmiHeader.biHeight = -height; // トップダウン形式
            bmpInfo.bmiHeader.biPlanes = 1;
            bmpInfo.bmiHeader.biBitCount = 32; // BGRA
            bmpInfo.bmiHeader.biCompression = 0; // BI_RGB

            // ビットマップデータを取得
            int stride = width * 4; // BGRA = 4 bytes per pixel
            int bufferSize = stride * height;
            byte[] buffer = new byte[bufferSize];

            HDC hdc = PInvoke.GetDC(HWND.Null);
            try
            {
                // fixedステートメントでバッファを固定
                fixed (byte* pBuffer = buffer)
                {
                    // BITMAPINFOのアドレスを取得（構造体なので、fixedブロック内でアドレスを取得可能）
                    BITMAPINFO* pBmpInfo = &bmpInfo;
                    
                    int lines = PInvoke.GetDIBits(
                        hdc,
                        hBitmap,
                        0,
                        (uint)height,
                        pBuffer,
                        pBmpInfo,
                        0 // DIB_RGB_COLORS
                    );

                    if (lines == 0)
                    {
                        throw new Exception($"GetDIBitsに失敗しました (エラーコード: {Marshal.GetLastWin32Error()})");
                    }
                }
            }
            finally
            {
                PInvoke.ReleaseDC(HWND.Null, hdc);
            }

            return buffer;
        }

        /// <summary>
        /// ビットマップデータをSoftwareBitmapに変換
        /// </summary>
        private Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(byte[] bitmapData, int width, int height)
        {
            // BGRA形式のデータから直接SoftwareBitmapを作成
            int stride = width * 4; // BGRA = 4 bytes per pixel
            
            // バッファを作成
            var buffer = bitmapData.AsBuffer();
            
            // SoftwareBitmapを作成
            var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
                buffer,
                BitmapPixelFormat.Bgra8,
                width,
                height,
                BitmapAlphaMode.Premultiplied
            );
            
            return Task.FromResult(softwareBitmap);
        }

        // リトルエンディアンで書き込むヘルパーメソッド
        private void WriteUInt32LittleEndian(byte[] buffer, ref int offset, uint value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
            buffer[offset++] = (byte)((value >> 16) & 0xFF);
            buffer[offset++] = (byte)((value >> 24) & 0xFF);
        }

        private void WriteInt32LittleEndian(byte[] buffer, ref int offset, int value)
        {
            WriteUInt32LittleEndian(buffer, ref offset, (uint)value);
        }

        private void WriteUInt16LittleEndian(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary>
        /// 画像の主要色を取得
        /// Python版と同じアルゴリズムを使用: 中央部分のピクセルをサンプリングし、量子化して最も頻繁な色を取得
        /// </summary>
        public Windows.UI.Color GetDominantColor(SoftwareBitmap bitmap)
        {
            try
            {
                Logger.Debug("主要色の計算を開始します");

                // 最後にキャプチャしたbitmapDataを使用（既に取得済み）
                byte[]? bitmapData = _lastCapturedBitmapData;
                if (bitmapData == null)
                {
                    // フォールバック: 白色を返す
                    Logger.Warning("ピクセルデータが取得されていません。白色を返します。");
                    return new Windows.UI.Color { A = 255, R = 255, G = 255, B = 255 };
                }

                int width = _lastCapturedWidth;
                int height = _lastCapturedHeight;

                int stride = width * 4; // BGRA形式

                // 画像をリサイズして処理を高速化（Python版では50x50にリサイズ）
                int sampleWidth = Math.Min(50, width);
                int sampleHeight = Math.Min(50, height);
                int sampleStepX = Math.Max(1, width / sampleWidth);
                int sampleStepY = Math.Max(1, height / sampleHeight);

                // 中央部分の範囲を計算（1/4から3/4の範囲）
                int centerXStart = width / 4;
                int centerXEnd = 3 * width / 4;
                int centerYStart = height / 4;
                int centerYEnd = 3 * height / 4;

                // 色のカウント用の辞書（量子化後の色をキーとして使用）
                var colorCounts = new Dictionary<(byte, byte, byte), int>();

                // 中央部分のピクセルをサンプリング
                for (int y = centerYStart; y < centerYEnd; y += sampleStepY)
                {
                    for (int x = centerXStart; x < centerXEnd; x += sampleStepX)
                    {
                        int offset = y * stride + x * 4; // BGRA形式
                        if (offset + 3 < bitmapData.Length)
                        {
                            // BGRA形式で読み取る
                            byte b = bitmapData[offset];
                            byte g = bitmapData[offset + 1];
                            byte r = bitmapData[offset + 2];
                            // byte a = bitmapData[offset + 3]; // アルファチャンネルは使用しない

                            // 色を量子化（32で割って切り捨て、Python版と同じ）
                            byte quantizedR = (byte)((r / 32) * 32);
                            byte quantizedG = (byte)((g / 32) * 32);
                            byte quantizedB = (byte)((b / 32) * 32);

                            var quantizedColor = (quantizedR, quantizedG, quantizedB);
                            if (colorCounts.ContainsKey(quantizedColor))
                            {
                                colorCounts[quantizedColor]++;
                            }
                            else
                            {
                                colorCounts[quantizedColor] = 1;
                            }
                        }
                    }
                }

                // 最も頻繁に出現する色を取得
                if (colorCounts.Count > 0)
                {
                    var dominantColor = colorCounts.OrderByDescending(kvp => kvp.Value).First().Key;
                    Logger.Info($"主要色を計算しました: R={dominantColor.Item1}, G={dominantColor.Item2}, B={dominantColor.Item3}");
                    return new Windows.UI.Color
                    {
                        A = 255,
                        R = dominantColor.Item1,
                        G = dominantColor.Item2,
                        B = dominantColor.Item3
                    };
                }
                else
                {
                    // 色が見つからない場合は白色を返す
                    Logger.Warning("主要色が見つかりませんでした。白色を返します。");
                    return new Windows.UI.Color { A = 255, R = 255, G = 255, B = 255 };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("主要色の計算エラー", ex);
                // エラー時は白色を返す
                return new Windows.UI.Color { A = 255, R = 255, G = 255, B = 255 };
            }
        }

        /// <summary>
        /// SoftwareBitmapをPNGファイルに保存（デバッグ用）
        /// </summary>
        public async Task<string> SaveBitmapToFileAsync(SoftwareBitmap bitmap, string fileName = "capture.png")
        {
            try
            {
                // 保存先ディレクトリを取得（実行ディレクトリ）
                var appDirectory = AppContext.BaseDirectory;
                var filePath = Path.Combine(appDirectory, fileName);
                
                // ファイルを作成
                StorageFile file;
                try
                {
                    // 既存のファイルを取得しようと試みる
                    file = await StorageFile.GetFileFromPathAsync(filePath);
                    // 既存のファイルを削除して新規作成
                    await file.DeleteAsync();
                }
                catch
                {
                    // ファイルが存在しない場合はそのまま続行
                }
                
                // フォルダを取得してファイルを作成
                var folder = await StorageFolder.GetFolderFromPathAsync(appDirectory);
                file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                
                // SoftwareBitmapをPNG形式で保存
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetSoftwareBitmap(bitmap);
                    await encoder.FlushAsync();
                }
                
                Logger.Info($"キャプチャ画像を保存しました: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Error("画像の保存エラー", ex);
                throw;
            }
        }

        public void Dispose()
        {
            // リソースの解放が必要な場合はここに記述
            // 現在は特にリソースの解放は不要
        }
    }
}

