"""
Overlay Translator - メインアプリケーション
"""
import sys
import threading
from app.tray import TrayApp
from app.hotkey import HotkeyManager
from app.capture import ScreenCapture
from app.ocr import OCRProcessor
from app.translator import Translator
from app.overlay import OverlayWindow
from app.settings_window import SettingsWindow
from app.logger import debug, info, warning, error, exception

class OverlayTranslator:
    def __init__(self):
        self.capture = ScreenCapture()
        self.ocr = None
        self.translator = None
        self.overlay = None
        self.hotkey_manager = HotkeyManager()
        self.tray_app = None
        self.settings_window = None
        self._init_services()
    
    def _init_services(self):
        """OCRとTranslatorサービスを初期化"""
        try:
            self.ocr = OCRProcessor()
        except Exception as e:
            error(f"OCRの初期化に失敗しました: {e}")
            self.ocr = None
        
        try:
            self.translator = Translator()
        except Exception as e:
            error(f"Translatorの初期化に失敗しました: {e}")
            self.translator = None
        
    def on_selection_complete(self, x, y, width, height):
        """矩形選択完了時のコールバック"""
        try:
            debug(f"矩形選択完了: x={x}, y={y}, width={width}, height={height}")
            
            # サービスが初期化されているか確認
            if not self.ocr:
                error("OCRが初期化されていません。設定を確認してください。")
                return
            
            if not self.translator:
                error("Translatorが初期化されていません。設定を確認してください。")
                return
            
            debug("画面キャプチャを開始...")
            # 画面キャプチャ
            image = self.capture.capture_region(x, y, width, height)
            debug(f"キャプチャ完了: 画像サイズ={image.size}")
            
            debug("OCR処理を開始...")
            # OCR処理
            text = self.ocr.extract_text(image)
            if not text:
                warning("テキストが検出されませんでした")
                return
            debug(f"OCR完了: テキスト長={len(text)}文字")
            info(f"[OCR結果] {text}")
            
            debug("翻訳処理を開始...")
            # 翻訳
            translated = self.translator.translate_to_japanese(text)
            debug(f"翻訳完了: 翻訳テキスト長={len(translated)}文字")
            
            debug("背景色を取得...")
            # 背景色を取得
            bg_color = self.capture.get_dominant_color(image)
            debug(f"背景色: {bg_color}")
            
            debug("オーバーレイウィンドウを作成...")
            # オーバーレイ表示
            if self.overlay:
                debug("既存のオーバーレイを閉じる...")
                self.overlay.close()
            debug(f"新しいオーバーレイを作成: x={x}, y={y}, width={width}, height={height}")
            self.overlay = OverlayWindow(x, y, width, height, translated, bg_color)
            debug("オーバーレイを表示...")
            self.overlay.show()
            debug("処理完了")
            
        except Exception as e:
            exception("エラーが発生しました", exc_info=True)
    
    def start(self):
        """アプリケーションを起動"""
        # ホットキー登録
        self.hotkey_manager.register_hotkey(
            callback=self.on_selection_complete
        )
        
        # タスクトレイ起動
        self.tray_app = TrayApp(
            on_quit=self.stop,
            on_settings=self.show_settings
        )
        
        # ホットキー監視を別スレッドで開始
        hotkey_thread = threading.Thread(
            target=self.hotkey_manager.start_listening,
            daemon=True
        )
        hotkey_thread.start()
        
        # タスクトレイをメインスレッドで実行
        self.tray_app.run()
    
    def stop(self):
        """アプリケーションを終了"""
        try:
            # クリーンアップ処理
            if self.overlay:
                self.overlay.close()
            if self.settings_window:
                self.settings_window.close()
            self.hotkey_manager.stop()
        except Exception as e:
            error(f"クリーンアップエラー: {e}")
        finally:
            # アプリケーションを終了
            import os
            os._exit(0)  # sys.exit()の代わりにos._exit()を使用（例外を発生させない）
    
    def show_settings(self):
        """設定画面を表示"""
        # 既存の設定画面が開いている場合は前面に表示
        if self.settings_window and self.settings_window.root:
            try:
                # ウィンドウがまだ存在するか確認
                self.settings_window.root.winfo_exists()
                self.settings_window.root.lift()
                self.settings_window.root.focus_force()
                self.settings_window.root.attributes('-topmost', True)
                self.settings_window.root.after(100, lambda: self.settings_window.root.attributes('-topmost', False))
                return
            except:
                # ウィンドウが既に閉じられている場合は参照をクリア
                self.settings_window = None
        
        # 設定画面を別スレッドで開く（Tkinterは別スレッドでも動作する）
        def open_settings():
            self.settings_window = SettingsWindow(on_save_callback=self._on_settings_saved)
            self.settings_window.show()
            # ウィンドウが閉じられた後、参照をクリア
            self.settings_window = None
        
        settings_thread = threading.Thread(target=open_settings, daemon=True)
        settings_thread.start()
    
    def _on_settings_saved(self):
        """設定保存後のコールバック"""
        # OCRとTranslatorを再初期化
        self._init_services()
        
        # ホットキーを更新
        import os
        from dotenv import load_dotenv
        load_dotenv(override=True)
        new_hotkey = os.getenv('HOTKEY', 'ctrl+shift+t')
        if self.hotkey_manager.update_hotkey(new_hotkey):
            info(f"ホットキーを更新しました: {new_hotkey}")
        else:
            warning(f"ホットキーの更新に失敗しました。現在のホットキー: {self.hotkey_manager.hotkey}")
        
        # 設定画面の参照をクリア
        self.settings_window = None

if __name__ == "__main__":
    app = OverlayTranslator()
    app.start()

