"""
画面キャプチャ機能
"""
import mss
from PIL import Image
import numpy as np
from collections import Counter
import threading
from app.logger import debug, error, exception

class ScreenCapture:
    def __init__(self):
        # mssはスレッドローカルストレージを使用するため、
        # 各スレッドで個別にインスタンスを作成する必要がある
        self._local = threading.local()
    
    def _get_mss_instance(self):
        """現在のスレッド用のmssインスタンスを取得"""
        if not hasattr(self._local, 'sct'):
            self._local.sct = mss.mss()
        return self._local.sct
    
    def capture_region(self, x, y, width, height):
        """指定された領域をキャプチャ"""
        debug(f"キャプチャ開始: スレッド={threading.current_thread().name}")
        try:
            # 現在のスレッド用のmssインスタンスを取得
            sct = self._get_mss_instance()
            debug("mssインスタンス取得完了")
            
            monitor = {
                "top": y,
                "left": x,
                "width": width,
                "height": height
            }
            
            debug(f"画面キャプチャ実行: monitor={monitor}")
            screenshot = sct.grab(monitor)
            debug(f"キャプチャ完了: サイズ={screenshot.size}")
            
            image = Image.frombytes("RGB", screenshot.size, screenshot.bgra, "raw", "BGRX")
            debug(f"画像変換完了: サイズ={image.size}")
            
            return image
        except Exception as e:
            exception("キャプチャエラー", exc_info=True)
            raise
    
    def get_dominant_color(self, image, k=1):
        """画像の主要な背景色を取得"""
        # 画像をリサイズして処理を高速化
        image_small = image.resize((50, 50))
        
        # 画像をnumpy配列に変換
        img_array = np.array(image_small)
        
        # ピクセルを1次元配列に変換
        pixels = img_array.reshape(-1, 3)
        
        # 最も頻繁に出現する色を取得（背景色と仮定）
        # エッジ部分を除外して中央部分の色を取得
        h, w = img_array.shape[:2]
        center_h_start = h // 4
        center_h_end = 3 * h // 4
        center_w_start = w // 4
        center_w_end = 3 * w // 4
        
        center_pixels = img_array[center_h_start:center_h_end, center_w_start:center_w_end].reshape(-1, 3)
        
        # 色を量子化（近い色をグループ化）
        quantized = (center_pixels // 32) * 32
        
        # 最も頻繁な色を取得
        color_counts = Counter(tuple(c) for c in quantized)
        dominant_color = color_counts.most_common(1)[0][0]
        
        # RGB値を返す
        return f"#{dominant_color[0]:02x}{dominant_color[1]:02x}{dominant_color[2]:02x}"

