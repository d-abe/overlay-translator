"""
オーバーレイ表示機能
"""
import os
import tkinter as tk
from tkinter_unblur import Tk
import threading
from dotenv import load_dotenv
from app.logger import debug, warning, error, exception

class OverlayWindow:
    def __init__(self, x, y, width, height, text, bg_color):
        self.x = x
        self.y = y
        self.width = width
        self.height = height
        self.text = text
        self.bg_color = bg_color
        
        # 環境変数からフォント設定を読み込む
        load_dotenv(override=True)
        self.font_family = os.getenv('OVERLAY_FONT_FAMILY', 'Meiryo')
        self.font_size = int(os.getenv('OVERLAY_FONT_SIZE', '12'))
        self.font_style = os.getenv('OVERLAY_FONT_STYLE', 'normal')
        
        self.root = None
        self._create_window()
    
    def _create_window(self):
        """オーバーレイウィンドウを作成"""
        # 別スレッドで独立したTkinterルートを作成
        # 各オーバーレイウィンドウは独立したTkinterインスタンスを持つ
        def create_in_thread():
            try:
                import sys
                import threading
                debug(f"オーバーレイウィンドウ作成スレッド開始: {threading.current_thread().name}")
                debug(f"プラットフォーム: {sys.platform}")
                
                # WindowsでのTkinter初期化を明示的に行う
                if sys.platform == 'win32':
                    debug("Windows環境: Tkinter初期化を試行...")
                    try:
                        # スレッドローカルストレージを初期化
                        import _tkinter
                        debug("_tkinterモジュールをインポート")
                        # Tkinterの内部状態をリセット
                        if hasattr(_tkinter, '_test'):
                            debug("_tkinter._test()を実行")
                            _tkinter._test()
                    except Exception as e:
                        warning(f"Tkinter初期化エラー（無視）: {e}")
                
                debug("Tkインスタンスを作成...")
                # Tkインスタンスを作成（Windowsでは別スレッドで作成する際に問題が発生する可能性がある）
                try:
                    self.root = Tk()
                    debug("Tkインスタンス作成完了")
                except AttributeError as e:
                    error(f"Tkインスタンス作成時のAttributeError: {e}")
                    error(f"エラーの詳細: {type(e).__name__}: {str(e)}")
                    exception("Tkインスタンス作成エラー", exc_info=True)
                    raise
                
                debug("ウィンドウ属性を設定...")
                try:
                    self.root.overrideredirect(True)
                    debug("overrideredirect設定完了")
                    self.root.attributes('-topmost', True)
                    debug("topmost設定完了")
                    self.root.attributes('-alpha', 0.9)
                    debug("alpha設定完了")
                except Exception as e:
                    exception("ウィンドウ属性設定エラー", exc_info=True)
                    raise
                debug("ウィンドウ属性設定完了")
                
                debug("テキスト色を計算...")
                # 背景色に合わせたテキスト色を計算
                text_color = self._get_contrast_color(self.bg_color)
                debug(f"テキスト色: {text_color}")
                
                debug("フレームを作成...")
                try:
                    # フレームを作成
                    frame = tk.Frame(
                        self.root,
                        bg=self.bg_color,
                        padx=10,
                        pady=10
                    )
                    frame.pack(fill=tk.BOTH, expand=True)
                    debug("フレーム作成完了")
                except Exception as e:
                    exception("フレーム作成エラー", exc_info=True)
                    raise
                
                debug("閉じるボタンを作成...")
                try:
                    # 閉じるボタン（rootウィンドウに直接配置してテキストを隠さないようにする）
                    close_btn = tk.Button(
                        self.root,
                        text="×",
                        command=self.close,
                        bg=self.bg_color,
                        fg=text_color,
                        font=('Arial', 14, 'bold'),
                        relief=tk.FLAT,
                        borderwidth=1,
                        cursor='hand2',
                        width=2,
                        height=1
                    )
                    debug("閉じるボタン作成完了")
                except Exception as e:
                    exception("閉じるボタン作成エラー", exc_info=True)
                    raise
                
                debug("テキストウィジェットを作成...")
                try:
                    # テキストを表示（閉じるボタンの下に来ないように上部マージンを追加）
                    text_widget = tk.Text(
                        frame,
                        bg=self.bg_color,
                        fg=text_color,
                        font=(self.font_family, self.font_size, self.font_style),
                        wrap=tk.WORD,
                        relief=tk.FLAT,
                        borderwidth=0,
                        padx=5,
                        pady=5
                    )
                    # packのpadyオプションで上部マージンを追加（閉じるボタンの下に来ないように）
                    text_widget.pack(fill=tk.BOTH, expand=True, pady=(30, 5))
                    text_widget.insert('1.0', self.text)
                    text_widget.config(state=tk.DISABLED)  # 読み取り専用
                    debug("テキストウィジェット作成完了")
                except Exception as e:
                    exception("テキストウィジェット作成エラー", exc_info=True)
                    raise
                
                debug("テキストサイズを測定してウィンドウサイズを調整...")
                # リサイズハンドルを外側のスコープで定義（後で参照するため）
                resize_handle = None
                try:
                    # まず、選択範囲のサイズでウィンドウを作成
                    self.root.geometry(f"{self.width}x{self.height}+{self.x}+{self.y}")
                    self.root.update_idletasks()  # レイアウトを更新
                    
                    # フォントメトリクスを取得してテキストサイズを計算
                    import tkinter.font as tkfont
                    # フォントスタイルをweightとslantに変換
                    weight = 'bold' if 'bold' in self.font_style else 'normal'
                    slant = 'italic' if 'italic' in self.font_style else 'roman'
                    font_obj = tkfont.Font(family=self.font_family, size=self.font_size, weight=weight, slant=slant)
                    
                    # テキストを改行で分割
                    lines = self.text.split('\n')
                    num_lines = len(lines)
                    
                    # 各行の実際の幅を計算（日本語文字は全角として扱う）
                    def get_line_width(line):
                        width = 0
                        for char in line:
                            # 日本語文字（全角）かどうかを判定
                            if ord(char) > 127:  # ASCII以外は全角として扱う
                                width += font_obj.measure('あ')  # 日本語文字の幅
                            else:
                                width += font_obj.measure(char)
                        return width
                    
                    # 各行の幅を計算して最大値を取得
                    max_line_width = max(get_line_width(line) for line in lines) if lines else 0
                    line_height = font_obj.metrics('linespace')
                    
                    # フレームのパディングを考慮
                    frame_padding_x = 20  # padx
                    frame_padding_y = 20  # pady
                    text_padding_x = 10  # text_widgetのpadx
                    text_padding_y_top = 30  # text_widgetの上部pady（閉じるボタンの下に来ないように）
                    text_padding_y_bottom = 5  # text_widgetの下部pady
                    close_btn_width = 25  # 閉じるボタンの幅
                    close_btn_height = 25  # 閉じるボタンの高さ
                    close_btn_margin = 5  # 閉じるボタンの右側・上側マージン
                    
                    # テキストの必要なサイズを計算（横幅は元のサイズ固定、高さはテキストに合わせる）
                    text_width = self.width - frame_padding_x - text_padding_x
                    text_height = num_lines * line_height
                    
                    # 必要な高さを計算（幅は元のサイズ固定、閉じるボタンの高さも考慮）
                    required_height = text_height + frame_padding_y + text_padding_y_top + text_padding_y_bottom + 10  # 少し余裕を持たせる
                    
                    # 幅は元の矩形選択サイズを固定で使用
                    final_width = self.width
                    final_height = max(self.height, required_height)
                    
                    # 画面サイズを取得して、はみ出さないように調整
                    screen_width = self.root.winfo_screenwidth()
                    screen_height = self.root.winfo_screenheight()
                    
                    # 位置を調整（画面からはみ出さないように）
                    final_x = self.x
                    final_y = self.y
                    
                    if final_x + final_width > screen_width:
                        final_x = screen_width - final_width - 10  # 10pxのマージン
                        if final_x < 0:
                            final_x = 10
                            # 幅は固定のため、位置のみ調整
                    
                    if final_y + final_height > screen_height:
                        final_y = screen_height - final_height - 10  # 10pxのマージン
                        if final_y < 0:
                            final_y = 10
                            final_height = screen_height - 20  # 画面高さに合わせる
                    
                    debug(f"元のサイズ: {self.width}x{self.height}")
                    debug(f"テキスト行数: {num_lines}, 最大行幅: {max_line_width}")
                    debug(f"計算されたテキストサイズ: {text_width}x{text_height}")
                    debug(f"最終サイズ: {final_width}x{final_height}")
                    debug(f"最終位置: {final_x}+{final_y}")
                    
                    # ウィンドウの位置とサイズを設定
                    self.root.geometry(f"{final_width}x{final_height}+{final_x}+{final_y}")
                    
                    # 閉じるボタンの位置を更新（右上角に配置）
                    close_btn.place(x=final_width-close_btn_width-close_btn_margin, y=close_btn_margin, width=close_btn_width, height=close_btn_height)
                    
                    # リサイズハンドルを作成（右下角）
                    resize_handle_size = 15
                    resize_handle = tk.Frame(
                        frame,
                        bg=self.bg_color,
                        width=resize_handle_size,
                        height=resize_handle_size,
                        cursor='sizing'
                    )
                    resize_handle.place(x=final_width-resize_handle_size-close_btn_margin-5, y=final_height-resize_handle_size-5)
                    
                    # リサイズ機能を実装
                    self._resize_data = {'x': 0, 'y': 0, 'width': final_width, 'height': final_height}
                    
                    def start_resize(event):
                        self._resize_data['x'] = event.x_root
                        self._resize_data['y'] = event.y_root
                        self._resize_data['width'] = self.root.winfo_width()
                        self._resize_data['height'] = self.root.winfo_height()
                    
                    def do_resize(event):
                        if not self._resize_data.get('x'):
                            return
                        dx = event.x_root - self._resize_data['x']
                        dy = event.y_root - self._resize_data['y']
                        new_width = max(200, self._resize_data['width'] + dx)  # 最小幅200px
                        new_height = max(100, self._resize_data['height'] + dy)  # 最小高さ100px
                        
                        # 画面サイズを超えないように
                        screen_width = self.root.winfo_screenwidth()
                        screen_height = self.root.winfo_screenheight()
                        current_x = self.root.winfo_x()
                        current_y = self.root.winfo_y()
                        
                        if current_x + new_width > screen_width:
                            new_width = screen_width - current_x - 10
                        if current_y + new_height > screen_height:
                            new_height = screen_height - current_y - 10
                        
                        self.root.geometry(f"{new_width}x{new_height}")
                        # 閉じるボタンの位置を更新
                        close_btn.place(x=new_width-close_btn_width-close_btn_margin, y=close_btn_margin, width=close_btn_width, height=close_btn_height)
                        # リサイズハンドルの位置を更新
                        resize_handle.place(x=new_width-resize_handle_size-close_btn_margin-5, y=new_height-resize_handle_size-5)
                    
                    def stop_resize(event):
                        self._resize_data['x'] = 0
                        self._resize_data['y'] = 0
                    
                    resize_handle.bind('<Button-1>', start_resize)
                    resize_handle.bind('<B1-Motion>', do_resize)
                    resize_handle.bind('<ButtonRelease-1>', stop_resize)
                    
                    # 再度レイアウトを更新
                    self.root.update_idletasks()
                    
                    debug("ウィンドウサイズ調整完了")
                except Exception as e:
                    exception("ウィンドウサイズ調整エラー", exc_info=True)
                    # エラーが発生した場合は元のサイズを使用
                    self.root.geometry(f"{self.width}x{self.height}+{self.x}+{self.y}")
                    close_btn.place(x=self.width-close_btn_width-close_btn_margin, y=close_btn_margin, width=close_btn_width, height=close_btn_height)
                
                debug("イベントバインディングを設定...")
                try:
                    # クリックで閉じる（閉じるボタンとリサイズハンドル以外）
                    def on_click(event):
                        # クリック位置が閉じるボタンやリサイズハンドルの領域でない場合のみ閉じる
                        widget = event.widget
                        if widget == close_btn or widget == resize_handle:
                            return
                        # フレーム内のクリックのみ閉じる
                        if widget == frame:
                            self.close()
                    
                    frame.bind('<Button-1>', on_click)
                    debug("イベントバインディング設定完了")
                except Exception as e:
                    exception("イベントバインディング設定エラー", exc_info=True)
                    raise
                
                debug("ウィンドウを表示...")
                try:
                    # ウィンドウを表示
                    self.root.deiconify()
                    debug("ウィンドウ表示完了")
                except Exception as e:
                    exception("ウィンドウ表示エラー", exc_info=True)
                    raise
                
                debug("メインループを開始...")
                try:
                    # メインループを開始
                    self.root.mainloop()
                    debug("メインループ終了")
                except Exception as e:
                    exception("メインループエラー", exc_info=True)
                    raise
            except Exception as e:
                import traceback
                exception("オーバーレイウィンドウの作成エラー", exc_info=True)
        
        # 別スレッドでウィンドウを作成・実行
        thread = threading.Thread(target=create_in_thread, daemon=True)
        thread.start()
    
    def _get_contrast_color(self, hex_color):
        """背景色に合わせたコントラストの高いテキスト色を取得"""
        # 16進数カラーコードをRGBに変換
        hex_color = hex_color.lstrip('#')
        r = int(hex_color[0:2], 16)
        g = int(hex_color[2:4], 16)
        b = int(hex_color[4:6], 16)
        
        # 輝度を計算
        luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255
        
        # 輝度に応じて白または黒を返す
        return '#FFFFFF' if luminance < 0.5 else '#000000'
    
    def show(self):
        """ウィンドウを表示（既に表示されている）"""
        pass
    
    def close(self):
        """ウィンドウを閉じる"""
        if self.root:
            try:
                self.root.quit()
                self.root.destroy()
            except:
                pass
            self.root = None

