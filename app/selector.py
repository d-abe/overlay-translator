"""
矩形選択機能
"""
import tkinter as tk
from tkinter import Canvas
from tkinter_unblur import Tk
import sys
import keyboard
import threading
from app.logger import debug, warning, error

class RectangleSelector:
    def __init__(self, callback):
        self.callback = callback
        self.root = None
        self.canvas = None
        self.start_x = 0
        self.start_y = 0
        self.rect_id = None
        self.is_selecting = False
        self.escape_pressed = False
        self.escape_hook = None
        self.is_closing = False  # 閉じる処理中フラグ
    
    def _get_screen_size(self):
        """実際の画面サイズを取得（DPIスケーリングを考慮）"""
        if sys.platform == 'win32':
            try:
                import ctypes
                from ctypes import wintypes
                
                # GetSystemMetricsを使用して実際の画面サイズを取得
                user32 = ctypes.windll.user32
                screen_width = user32.GetSystemMetrics(0)  # SM_CXSCREEN
                screen_height = user32.GetSystemMetrics(1)  # SM_CYSCREEN
                
                # マルチモニター対応：GetSystemMetrics(78)で仮想画面サイズを取得
                virtual_width = user32.GetSystemMetrics(78)  # SM_CXVIRTUALSCREEN
                virtual_height = user32.GetSystemMetrics(79)  # SM_CYVIRTUALSCREEN
                
                # 仮想画面サイズが大きい場合はそれを使用
                if virtual_width > screen_width:
                    screen_width = virtual_width
                if virtual_height > screen_height:
                    screen_height = virtual_height
                
                return screen_width, screen_height
            except Exception as e:
                warning(f"Windows APIで画面サイズ取得失敗: {e}")
                # フォールバック：Tkinterの方法を使用
                if self.root:
                    return self.root.winfo_screenwidth(), self.root.winfo_screenheight()
                return 1920, 1080  # デフォルト値
        else:
            # 非Windows環境
            if self.root:
                return self.root.winfo_screenwidth(), self.root.winfo_screenheight()
            return 1920, 1080  # デフォルト値
    
    def _on_escape_global(self):
        """グローバルESCキーイベントハンドラ"""
        debug("ESCキーが検出されました")
        if self.is_closing:
            debug("既に閉じる処理中です")
            return  # 既に閉じる処理中
        self.escape_pressed = True
        if self.root:
            try:
                debug("_on_escapeをスケジュールします")
                self.root.after(0, self._on_escape, None)
            except Exception as e:
                error(f"_on_escapeスケジュールエラー: {e}")
        else:
            warning("rootがNoneです")
    
    def _set_window_focus(self):
        """ウィンドウにフォーカスを設定（Windows API使用）"""
        if sys.platform == 'win32' and self.root:
            try:
                import ctypes
                from ctypes import wintypes
                
                # ウィンドウハンドルを取得
                hwnd = self.root.winfo_id()
                
                # Windows APIを使用してウィンドウをアクティブにする
                user32 = ctypes.windll.user32
                
                # SetForegroundWindow: ウィンドウを前面に表示
                user32.SetForegroundWindow(hwnd)
                
                # SetFocus: ウィンドウにフォーカスを設定
                user32.SetFocus(hwnd)
                
                # BringWindowToTop: ウィンドウを最前面に
                user32.BringWindowToTop(hwnd)
                
                # SetActiveWindow: ウィンドウをアクティブにする
                user32.SetActiveWindow(hwnd)
                
                debug("ウィンドウにフォーカスを設定しました")
            except Exception as e:
                warning(f"フォーカス設定エラー: {e}")
    
    def start_selection(self):
        """矩形選択を開始"""
        # 状態をリセット（前回のセッションの残りをクリア）
        self.is_closing = False
        self.is_selecting = False
        self.escape_pressed = False
        self.rect_id = None
        self.start_x = 0
        self.start_y = 0
        
        # フルスクリーンウィンドウを作成
        self.root = Tk()
        
        # 実際の画面サイズを取得（DPI対応）
        screen_width, screen_height = self._get_screen_size()
        
        debug(f"画面サイズ: {screen_width}x{screen_height}")
        
        # ウィンドウサイズと位置を明示的に設定
        self.root.geometry(f"{screen_width}x{screen_height}+0+0")
        self.root.attributes('-alpha', 0.3)  # 半透明
        self.root.attributes('-topmost', True)
        self.root.configure(bg='black')
        self.root.overrideredirect(True)
        
        # キャンバスを作成
        self.canvas = Canvas(
            self.root,
            highlightthickness=0,
            bg='black',
            width=screen_width,
            height=screen_height
        )
        self.canvas.pack(fill=tk.BOTH, expand=True)
        
        # レイアウトを更新して確実にサイズを適用
        self.root.update_idletasks()
        
        # ウィンドウにフォーカスを設定
        self._set_window_focus()
        
        # キャンバスにフォーカスを設定
        self.canvas.focus_set()
        self.canvas.focus_force()
        
        # イベントバインディング
        self.canvas.bind('<Button-1>', self._on_button_press)
        self.canvas.bind('<B1-Motion>', self._on_mouse_drag)
        self.canvas.bind('<ButtonRelease-1>', self._on_button_release)
        
        # ESCキーのバインディング（通常のキーバインディングを優先）
        # フォーカスがある場合に動作する通常のキーバインディング
        self.root.bind('<Escape>', self._on_escape)
        self.canvas.bind('<Escape>', self._on_escape)
        self.root.bind('<KeyPress-Escape>', self._on_escape)
        self.canvas.bind('<KeyPress-Escape>', self._on_escape)
        
        # フォーカスを確実に取得
        self.canvas.focus_set()
        self.canvas.focus_force()
        
        # ESCキーをグローバルホットキーとしても登録（フォールバック）
        try:
            # keyboardライブラリでESCキーを監視（add_hotkeyを使用）
            debug("ESCキーのグローバルホットキーを登録します（フォールバック）")
            self.escape_hook = keyboard.add_hotkey('esc', self._on_escape_global)
            debug("ESCキーのグローバルホットキー登録完了")
        except Exception as e:
            warning(f"グローバルESCキー登録失敗（通常のキーバインディングを使用）: {e}")
            self.escape_hook = None
        
        # カーソルを変更
        self.root.config(cursor='crosshair')
        self.canvas.config(cursor='crosshair')
        
        # ウィンドウを表示
        try:
            self.root.mainloop()
        finally:
            # クリーンアップ：すべてのイベントバインディングを解除
            try:
                if self.root and self.root.winfo_exists():
                    self.root.unbind('<Escape>')
                    self.root.unbind('<KeyPress-Escape>')
            except:
                pass
            
            try:
                if self.canvas:
                    self.canvas.unbind('<Escape>')
                    self.canvas.unbind('<KeyPress-Escape>')
                    self.canvas.unbind('<Button-1>')
                    self.canvas.unbind('<B1-Motion>')
                    self.canvas.unbind('<ButtonRelease-1>')
            except:
                pass
            
            # ESCキーのグローバルホットキーを解除
            if self.escape_hook:
                try:
                    debug("ESCキーフックを解除します")
                    # keyboard.add_hotkeyの戻り値は削除関数
                    if callable(self.escape_hook):
                        self.escape_hook()
                    debug("ESCキーフック解除完了")
                except Exception as e:
                    warning(f"ESCキーフック解除失敗: {e}")
            self.escape_hook = None
            
            # 参照をクリア
            self.canvas = None
            self.root = None
    
    def _on_button_press(self, event):
        """マウスボタンが押されたとき"""
        self.start_x = event.x_root
        self.start_y = event.y_root
        self.is_selecting = True
    
    def _on_mouse_drag(self, event):
        """マウスドラッグ中"""
        if not self.is_selecting:
            return
        
        # 既存の矩形を削除
        if self.rect_id:
            self.canvas.delete(self.rect_id)
        
        # 画面座標を取得
        current_x = event.x_root
        current_y = event.y_root
        
        # 矩形の座標を計算
        x1 = min(self.start_x, current_x)
        y1 = min(self.start_y, current_y)
        x2 = max(self.start_x, current_x)
        y2 = max(self.start_y, current_y)
        
        # ウィンドウの位置を取得
        root_x = self.root.winfo_rootx()
        root_y = self.root.winfo_rooty()
        
        # キャンバス座標に変換
        canvas_x1 = x1 - root_x
        canvas_y1 = y1 - root_y
        canvas_x2 = x2 - root_x
        canvas_y2 = y2 - root_y
        
        self.rect_id = self.canvas.create_rectangle(
            canvas_x1, canvas_y1, canvas_x2, canvas_y2,
            outline='red', width=2
        )
    
    def _on_button_release(self, event):
        """マウスボタンが離されたとき"""
        if not self.is_selecting:
            return
        
        end_x = event.x_root
        end_y = event.y_root
        
        # 選択範囲を計算
        x = min(self.start_x, end_x)
        y = min(self.start_y, end_y)
        width = abs(end_x - self.start_x)
        height = abs(end_y - self.start_y)
        
        # ウィンドウを閉じる
        self.root.quit()
        self.root.destroy()
        
        # コールバックを呼び出し
        if width > 10 and height > 10:  # 最小サイズチェック
            if self.callback:
                self.callback(x, y, width, height)
    
    def _on_escape(self, event=None):
        """ESCキーでキャンセル（通常のキーバインディング用）"""
        debug("_on_escapeが呼ばれました")
        
        # 既に閉じる処理中またはウィンドウが存在しない場合は何もしない
        if self.is_closing or not self.root:
            debug("既に閉じる処理中またはウィンドウが存在しません")
            return
        
        # ウィンドウがまだ存在するか確認
        try:
            if not self.root.winfo_exists():
                debug("ウィンドウが既に破棄されています")
                self.root = None
                return
        except Exception as e:
            warning(f"ウィンドウ存在確認エラー: {e}")
            self.root = None
            return
        
        self.is_closing = True
        debug("ウィンドウを閉じます")
        
        # イベントバインディングを解除（エラーを防ぐため）
        try:
            self.root.unbind('<Escape>')
            self.canvas.unbind('<Escape>')
            self.root.unbind('<KeyPress-Escape>')
            self.canvas.unbind('<KeyPress-Escape>')
        except Exception as e:
            warning(f"イベントバインディング解除エラー（無視）: {e}")
        
        # ウィンドウを閉じる
        try:
            debug("quit()を呼びます")
            self.root.quit()
        except Exception as e:
            warning(f"quit()エラー: {e}")
        
        try:
            debug("destroy()を呼びます")
            self.root.destroy()
            debug("destroy()完了")
        except Exception as e:
            warning(f"ウィンドウ破棄エラー（無視）: {e}")
        
        self.root = None
        debug("_on_escape完了")

