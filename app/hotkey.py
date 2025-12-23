"""
ホットキー管理と矩形選択機能
"""
import os
import keyboard
import threading
import time
from dotenv import load_dotenv
from app.logger import error, warning
from app.selector import RectangleSelector

class HotkeyManager:
    def __init__(self):
        # 環境変数からホットキーを読み込む
        load_dotenv(override=True)
        self.hotkey = os.getenv('HOTKEY', 'ctrl+shift+t').lower()
        self.listening = False
        self.selector = None
        self.callback = None
        self.current_hotkey_id = None
    
    def register_hotkey(self, callback, hotkey=None):
        """ホットキーを登録"""
        if hotkey:
            self.hotkey = hotkey.lower()
        elif not self.hotkey:
            # 環境変数から再読み込み
            load_dotenv(override=True)
            self.hotkey = os.getenv('HOTKEY', 'ctrl+shift+t').lower()
        self.callback = callback
    
    def _on_hotkey_pressed(self):
        """ホットキーが押されたときの処理"""
        if self.callback:
            # 矩形選択を開始
            self.selector = RectangleSelector(self.callback)
            self.selector.start_selection()
    
    def start_listening(self):
        """ホットキーの監視を開始"""
        import os
        from app.logger import debug
        debug(f"ホットキー監視を開始します (PID: {os.getpid()})")
        self.listening = True
        try:
            self.current_hotkey_id = keyboard.add_hotkey(self.hotkey, self._on_hotkey_pressed)
            debug(f"ホットキーを登録しました: {self.hotkey}")
        except Exception as e:
            error(f"ホットキーの登録に失敗しました: {e}")
            warning(f"使用しようとしたホットキー: {self.hotkey}")
            warning("デフォルトのホットキー（ctrl+shift+t）を使用します")
            self.hotkey = 'ctrl+shift+t'
            self.current_hotkey_id = keyboard.add_hotkey(self.hotkey, self._on_hotkey_pressed)
            debug(f"デフォルトホットキーを登録しました: {self.hotkey}")
        
        # 監視ループ
        debug("ホットキー監視ループを開始します")
        while self.listening:
            time.sleep(0.1)
    
    def update_hotkey(self, new_hotkey):
        """ホットキーを更新（実行中に変更する場合）"""
        if self.listening:
            # 既存のホットキーを保存（エラー時に戻すため）
            old_hotkey = self.hotkey
            # 既存のホットキーを削除
            keyboard.unhook_all()
            # 新しいホットキーを設定
            self.hotkey = new_hotkey.lower()
            try:
                self.current_hotkey_id = keyboard.add_hotkey(self.hotkey, self._on_hotkey_pressed)
                return True
            except Exception as e:
                error(f"ホットキーの更新に失敗しました: {e}")
                # 元のホットキーに戻す
                self.hotkey = old_hotkey
                try:
                    self.current_hotkey_id = keyboard.add_hotkey(self.hotkey, self._on_hotkey_pressed)
                except:
                    pass
                return False
        else:
            # まだ監視を開始していない場合は、単に設定を更新
            self.hotkey = new_hotkey.lower()
            return True
    
    def stop(self):
        """ホットキーの監視を停止"""
        self.listening = False
        keyboard.unhook_all()

