"""
タスクトレイ常駐機能
"""
import os
import sys
from pathlib import Path
import pystray
from PIL import Image, ImageDraw
import threading
from app.logger import warning, error

class TrayApp:
    def __init__(self, on_quit=None, on_settings=None):
        self.on_quit = on_quit
        self.on_settings = on_settings
        self.icon = None
        self._create_icon()
    
    def _get_icon_path(self):
        """icon.pngのパスを取得"""
        if getattr(sys, 'frozen', False):
            # exeファイルとして実行されている場合
            # PyInstallerのonefileモードでは、データファイルは一時ディレクトリに展開される
            # または、EXEと同じディレクトリから読み込む
            if hasattr(sys, '_MEIPASS'):
                # PyInstallerの一時ディレクトリ（データファイルが展開される場所）
                temp_path = Path(sys._MEIPASS) / 'icon.png'
                if temp_path.exists():
                    return str(temp_path)
            # EXEと同じディレクトリから読み込む（配布時にicon.pngを同梱する場合）
            exe_path = Path(sys.executable).parent / 'icon.png'
            if exe_path.exists():
                return str(exe_path)
            # 一時ディレクトリにフォールバック
            if hasattr(sys, '_MEIPASS'):
                return str(Path(sys._MEIPASS) / 'icon.png')
            return str(Path(sys.executable).parent / 'icon.png')
        else:
            # Pythonスクリプトとして実行されている場合
            base_path = Path(__file__).parent.parent
            icon_path = base_path / 'icon.png'
            return str(icon_path)
    
    def _create_icon(self):
        """タスクトレイアイコンを作成"""
        # icon.pngファイルを読み込む
        icon_path = self._get_icon_path()
        
        if os.path.exists(icon_path):
            try:
                # 画像ファイルを読み込む
                image = Image.open(icon_path)
                # タスクトレイ用に適切なサイズにリサイズ（通常16x16または32x32が推奨）
                # 64x64にリサイズして、必要に応じて縮小
                if image.size[0] > 64 or image.size[1] > 64:
                    image = image.resize((64, 64), Image.Resampling.LANCZOS)
                elif image.size[0] < 16 or image.size[1] < 16:
                    # 小さすぎる場合は拡大
                    image = image.resize((64, 64), Image.Resampling.LANCZOS)
                
                # RGBAモードに変換（透明度をサポート）
                if image.mode != 'RGBA':
                    image = image.convert('RGBA')
            except Exception as e:
                warning(f"icon.pngの読み込みに失敗しました: {e}")
                # フォールバック: デフォルトアイコンを生成
                image = self._create_default_icon()
        else:
            warning(f"icon.pngが見つかりません: {icon_path}")
            # フォールバック: デフォルトアイコンを生成
            image = self._create_default_icon()
        
        menu = pystray.Menu(
            pystray.MenuItem("設定", self._on_settings, default=True),
            pystray.MenuItem("終了", self._on_quit)
        )
        
        self.icon = pystray.Icon("OverlayTranslator", image, "Overlay Translator", menu)
    
    def _create_default_icon(self):
        """デフォルトアイコンを生成（フォールバック用）"""
        # シンプルな翻訳アイコンの画像を生成
        image = Image.new('RGBA', (64, 64), color=(255, 255, 255, 0))
        draw = ImageDraw.Draw(image)
        
        # 翻訳アイコンを描画（簡単な矢印と文字）
        draw.rectangle([10, 20, 30, 40], fill='blue', outline='black')
        draw.polygon([(35, 25), (50, 30), (35, 35)], fill='green')
        draw.text((15, 45), "翻", fill='black')
        
        return image
    
    def _on_quit(self, icon, item):
        """終了処理"""
        # アイコンを先に停止してから終了処理を呼び出す
        self.icon.stop()
        # 終了処理を別スレッドで実行（SystemExit例外を適切に処理）
        if self.on_quit:
            threading.Thread(target=self.on_quit, daemon=True).start()
    
    def _on_settings(self, icon, item):
        """設定画面を表示"""
        if self.on_settings:
            self.on_settings()
    
    def run(self):
        """タスクトレイを実行"""
        self.icon.run()

