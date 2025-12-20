"""
設定画面
"""
import os
import sys
import tkinter as tk
from tkinter import ttk, filedialog, messagebox, font
from tkinter_unblur import Tk
import threading
from pathlib import Path
from app.settings import SettingsManager
from app.logger import warning

class SettingsWindow:
    def __init__(self, on_save_callback=None):
        self.settings_manager = SettingsManager()
        self.on_save_callback = on_save_callback
        self.root = None
        self._tk_vars = []  # Tkinter変数の参照を保持
        self._create_window()
    
    def _create_window(self):
        """設定ウィンドウを作成"""
        self.root = Tk()
        self.root.title("設定 - Overlay Translator")
        self.root.geometry("500x700")
        self.root.minsize(500, 700)
        self.root.resizable(False, False)
        
        # ウィンドウサイズ変更を監視（必要に応じて有効化）
        # self.root.bind('<Configure>', self._on_window_configure)
        
        # アイコンを設定
        self._set_window_icon()
        
        # メインフレーム
        main_frame = ttk.Frame(self.root, padding="10")
        main_frame.pack(fill=tk.BOTH, expand=True)
        
        # 設定を読み込み
        settings = self.settings_manager.get_all()
        
        # Groq API設定セクション
        groq_frame = ttk.LabelFrame(main_frame, text="Groq API設定（翻訳用）", padding="10")
        groq_frame.pack(fill=tk.X, pady=5)
        
        ttk.Label(groq_frame, text="APIキー:").grid(row=0, column=0, sticky=tk.W, pady=5)
        self.groq_api_key_var = tk.StringVar(value=settings['GROQ_API_KEY'])
        self._tk_vars.append(self.groq_api_key_var)
        groq_api_entry = ttk.Entry(groq_frame, textvariable=self.groq_api_key_var, width=50, show="*")
        groq_api_entry.grid(row=0, column=1, padx=5, pady=5)
        
        ttk.Label(groq_frame, text="モデル:").grid(row=1, column=0, sticky=tk.W, pady=5)
        self.groq_model_var = tk.StringVar(value=settings['GROQ_MODEL'])
        self._tk_vars.append(self.groq_model_var)
        groq_model_combo = ttk.Combobox(
            groq_frame,
            textvariable=self.groq_model_var,
            values=[
                'llama-3.1-8b-instant',           # 高速・低コスト（推奨）
                'llama-3.3-70b-versatile',         # 高品質
                'openai/gpt-oss-120b',            # 最高品質
                'openai/gpt-oss-20b',             # 高速
                'meta-llama/llama-4-maverick-17b-128e-instruct',  # Preview
                'meta-llama/llama-4-scout-17b-16e-instruct',      # Preview
                'qwen/qwen3-32b',                 # Preview
            ],
            width=47,
            state='readonly'
        )
        groq_model_combo.grid(row=1, column=1, padx=5, pady=5)
        
        # Google Cloud Vision API設定セクション
        google_frame = ttk.LabelFrame(main_frame, text="Google Cloud Vision API設定（OCR用）", padding="10")
        google_frame.pack(fill=tk.X, pady=5)
        
        ttk.Label(google_frame, text="APIキー:").grid(row=0, column=0, sticky=tk.W, pady=5)
        self.google_api_key_var = tk.StringVar(value=settings['GOOGLE_API_KEY'])
        self._tk_vars.append(self.google_api_key_var)
        google_api_entry = ttk.Entry(google_frame, textvariable=self.google_api_key_var, width=50, show="*")
        google_api_entry.grid(row=0, column=1, padx=5, pady=5)
        
        ttk.Label(google_frame, text="サービスアカウント\nJSONファイル:").grid(row=1, column=0, sticky=tk.W, pady=5)
        self.google_creds_var = tk.StringVar(value=settings['GOOGLE_APPLICATION_CREDENTIALS'])
        self._tk_vars.append(self.google_creds_var)
        google_creds_frame = ttk.Frame(google_frame)
        google_creds_frame.grid(row=1, column=1, padx=5, pady=5, sticky=tk.EW)
        google_creds_entry = ttk.Entry(google_creds_frame, textvariable=self.google_creds_var, width=40)
        google_creds_entry.pack(side=tk.LEFT, fill=tk.X, expand=True)
        ttk.Button(google_creds_frame, text="参照...", command=self._browse_json_file).pack(side=tk.LEFT, padx=5)
        
        # OCR方法選択
        ocr_frame = ttk.LabelFrame(main_frame, text="OCR方法", padding="10")
        ocr_frame.pack(fill=tk.X, pady=5)
        
        self.ocr_method_var = tk.StringVar(value=settings['OCR_METHOD'])
        self._tk_vars.append(self.ocr_method_var)
        ttk.Radiobutton(
            ocr_frame,
            text="Google Cloud Vision API（高精度・請求が必要）",
            variable=self.ocr_method_var,
            value="google"
        ).pack(anchor=tk.W, pady=2)
        ttk.Radiobutton(
            ocr_frame,
            text="Groq Vision API（高速・Groq APIキー使用）",
            variable=self.ocr_method_var,
            value="groq"
        ).pack(anchor=tk.W, pady=2)
        ttk.Radiobutton(
            ocr_frame,
            text="EasyOCR（オフライン・初回のみモデルダウンロード）",
            variable=self.ocr_method_var,
            value="easyocr"
        ).pack(anchor=tk.W, pady=2)
        
        # ホットキー設定セクション
        hotkey_frame = ttk.LabelFrame(main_frame, text="ホットキー設定", padding="10")
        hotkey_frame.pack(fill=tk.X, pady=5)
        
        ttk.Label(hotkey_frame, text="ホットキー:").grid(row=0, column=0, sticky=tk.W, pady=5)
        self.hotkey_var = tk.StringVar(value=settings.get('HOTKEY', 'ctrl+shift+t'))
        self._tk_vars.append(self.hotkey_var)
        hotkey_entry = ttk.Entry(hotkey_frame, textvariable=self.hotkey_var, width=30)
        hotkey_entry.grid(row=0, column=1, padx=5, pady=5, sticky=tk.W)
        
        # ホットキーの説明
        help_text = "例: ctrl+shift+t, alt+f1, ctrl+alt+s\n複数のキーは「+」で区切ります"
        help_label = ttk.Label(hotkey_frame, text=help_text, font=('TkDefaultFont', 8), foreground='gray')
        help_label.grid(row=1, column=0, columnspan=2, sticky=tk.W, padx=5)
        
        # オーバーレイ表示設定セクション
        overlay_frame = ttk.LabelFrame(main_frame, text="オーバーレイ表示設定", padding="10")
        overlay_frame.pack(fill=tk.X, pady=5)
        
        ttk.Label(overlay_frame, text="フォント:").grid(row=0, column=0, sticky=tk.W, pady=5)
        
        # 現在のフォント設定を取得
        font_family = settings.get('OVERLAY_FONT_FAMILY', 'Meiryo')
        font_size = int(settings.get('OVERLAY_FONT_SIZE', '12'))
        font_style = settings.get('OVERLAY_FONT_STYLE', 'normal')
        
        # スタイル名のマッピング（表示用）
        style_display_map = {
            'normal': 'Regular',
            'bold': 'Bold',
            'italic': 'Italic',
            'bold italic': 'Bold Italic'
        }
        style_display = style_display_map.get(font_style, font_style)
        
        # フォント表示用のラベル
        self.font_display_var = tk.StringVar(value=f"{font_family}, {style_display}, {font_size}pt")
        font_display_label = ttk.Label(overlay_frame, textvariable=self.font_display_var, width=35)
        font_display_label.grid(row=0, column=1, padx=5, pady=5, sticky=tk.W)
        
        # フォント選択ボタン
        font_select_btn = ttk.Button(
            overlay_frame,
            text="フォントを選択...",
            command=self._select_font
        )
        font_select_btn.grid(row=0, column=2, padx=5, pady=5, sticky=tk.W)
        
        # フォント情報を保持（内部用）
        self.font_family_var = tk.StringVar(value=font_family)
        self.font_size_var = tk.StringVar(value=str(font_size))
        self.font_style_var = tk.StringVar(value=font_style)
        self._tk_vars.extend([self.font_family_var, self.font_size_var, self.font_style_var])
        
        # デバッグ設定セクション
        debug_frame = ttk.LabelFrame(main_frame, text="デバッグ設定", padding="10")
        debug_frame.pack(fill=tk.X, pady=5)
        
        self.debug_logging_var = tk.BooleanVar(value=settings.get('DEBUG_LOGGING', 'false').lower() in ('true', '1', 'yes', 'on'))
        debug_checkbox = ttk.Checkbutton(
            debug_frame,
            text="DEBUGログを有効にする",
            variable=self.debug_logging_var
        )
        debug_checkbox.pack(anchor=tk.W, pady=2)
        
        # ボタンフレーム
        button_frame = ttk.Frame(main_frame)
        button_frame.pack(fill=tk.X, pady=10)
        
        ttk.Button(button_frame, text="保存", command=self._save_settings).pack(side=tk.RIGHT, padx=5)
        ttk.Button(button_frame, text="キャンセル", command=self._close).pack(side=tk.RIGHT, padx=5)
        
        # ウィンドウが閉じられたときの処理
        self.root.protocol("WM_DELETE_WINDOW", self._close)
        
        # ウィンドウを中央に配置
        self.root.update_idletasks()
        width = self.root.winfo_width()
        height = self.root.winfo_height()
        x = (self.root.winfo_screenwidth() // 2) - (width // 2)
        y = (self.root.winfo_screenheight() // 2) - (height // 2)
        self.root.geometry(f'{width}x{height}+{x}+{y}')
        
        # ウィンドウを最前面に
        self.root.attributes('-topmost', True)
        self.root.after(100, lambda: self.root.attributes('-topmost', False))
    
    # ウィンドウサイズ変更時のイベントハンドラ（必要に応じて有効化）
    # def _on_window_configure(self, event):
    #     """ウィンドウサイズ変更時のイベントハンドラ"""
    #     # ウィンドウ自体のサイズ変更のみをログに出力（子ウィジェットの変更は無視）
    #     if event.widget == self.root:
    #         width = self.root.winfo_width()
    #         height = self.root.winfo_height()
    #         print(f"[設定ウィンドウサイズ] {width}x{height}")
    
    def _set_window_icon(self):
        """ウィンドウアイコンを設定"""
        try:
            # 実行ファイルと同じディレクトリを優先
            if getattr(sys, 'frozen', False):
                # exeファイルとして実行されている場合
                base_path = Path(sys.executable).parent
            else:
                # Pythonスクリプトとして実行されている場合
                base_path = Path(__file__).parent.parent
            
            icon_path = base_path / 'icon.png'
            if icon_path.exists():
                # PNGファイルを読み込んでアイコンとして設定
                # Tkinterは.icoファイルを直接サポートするが、PNGも使用可能
                self.root.iconphoto(False, tk.PhotoImage(file=str(icon_path)))
        except Exception as e:
            warning(f"アイコンの設定に失敗しました: {e}")
    
    def _browse_json_file(self):
        """JSONファイルを選択"""
        filename = filedialog.askopenfilename(
            title="サービスアカウントJSONファイルを選択",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if filename:
            self.google_creds_var.set(filename)
    
    def _select_font(self):
        """フォント選択ダイアログを表示"""
        try:
            # 現在のフォント設定を取得
            current_family = self.font_family_var.get()
            current_size = int(self.font_size_var.get())
            current_style = self.font_style_var.get()
            
            # フォント選択ダイアログを表示
            # Python 3.10以降では tkinter.fontchooser が利用可能
            try:
                from tkinter import fontchooser
                result = fontchooser.askfont(
                    parent=self.root,
                    family=current_family,
                    size=current_size
                )
                if result:
                    # フォントが選択された場合
                    self.font_family_var.set(result['family'])
                    self.font_size_var.set(str(result['size']))
                    # fontchooserの結果からスタイルを取得（利用可能な場合）
                    if 'weight' in result or 'slant' in result:
                        style = 'normal'
                        if result.get('weight') == 'bold' and result.get('slant') == 'italic':
                            style = 'bold italic'
                        elif result.get('weight') == 'bold':
                            style = 'bold'
                        elif result.get('slant') == 'italic':
                            style = 'italic'
                        self.font_style_var.set(style)
                    else:
                        self.font_style_var.set(current_style)  # 既存のスタイルを維持
                    
                    # 表示を更新
                    style_display_map = {
                        'normal': 'Regular',
                        'bold': 'Bold',
                        'italic': 'Italic',
                        'bold italic': 'Bold Italic'
                    }
                    style_display = style_display_map.get(self.font_style_var.get(), self.font_style_var.get())
                    self.font_display_var.set(f"{result['family']}, {style_display}, {result['size']}pt")
            except ImportError:
                # fontchooserが利用できない場合は、カスタムダイアログを使用
                self._show_custom_font_dialog()
        except Exception as e:
            messagebox.showerror("エラー", f"フォント選択に失敗しました: {e}")
    
    def _show_custom_font_dialog(self):
        """カスタムフォント選択ダイアログ（Windows標準のフォント選択ダイアログ風）"""
        dialog = tk.Toplevel(self.root)
        dialog.title("フォント")
        dialog.geometry("500x400")
        dialog.transient(self.root)
        dialog.grab_set()
        dialog.resizable(False, False)
        
        # 中央に配置
        dialog.update_idletasks()
        x = (dialog.winfo_screenwidth() // 2) - (500 // 2)
        y = (dialog.winfo_screenheight() // 2) - (400 // 2)
        dialog.geometry(f"500x400+{x}+{y}")
        
        # メインフレーム
        main_frame = ttk.Frame(dialog, padding="10")
        main_frame.pack(fill=tk.BOTH, expand=True)
        
        # 上部フレーム（フォント、スタイル、サイズの選択）
        top_frame = ttk.Frame(main_frame)
        top_frame.pack(fill=tk.BOTH, expand=True, pady=(0, 10))
        
        # フォント名（ユニークなフォントファミリー名のみを表示）
        font_frame = ttk.LabelFrame(top_frame, text="フォント名(F)", padding="5")
        font_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0, 5))
        
        # ユニークなフォントファミリー名を取得（重複を除去）
        all_fonts = font.families()
        unique_fonts = []
        seen = set()
        
        for font_name in sorted(all_fonts):
            # @で始まるフォント名は除外（縦書き用フォントなど）
            if font_name.startswith('@'):
                continue
            
            # フォント名を正規化（スタイル情報を含む可能性がある場合の処理）
            # 例: "Arial Bold" -> "Arial", "MS Gothic" -> "MS Gothic"
            base_name = font_name
            
            # 一般的なスタイル名を除去（Bold, Italic, Regular, Normal など）
            # ただし、フォント名の一部として含まれる場合は除外しない
            style_suffixes = [' Bold', ' Italic', ' Regular', ' Normal', ' Light', ' Medium', ' Heavy', ' Black']
            for suffix in style_suffixes:
                if base_name.endswith(suffix):
                    base_name = base_name[:-len(suffix)]
                    break
            
            # 既に見たフォント名はスキップ（重複除去）
            if base_name not in seen:
                seen.add(base_name)
                unique_fonts.append(base_name)
        
        font_family_var = tk.StringVar(value=self.font_family_var.get())
        # スクロールバーを作成
        font_scrollbar = tk.Scrollbar(font_frame, orient=tk.VERTICAL)
        font_listbox = tk.Listbox(font_frame, selectmode=tk.SINGLE, exportselection=False, yscrollcommand=font_scrollbar.set)
        font_scrollbar.config(command=font_listbox.yview)
        for font_name in unique_fonts:
            font_listbox.insert(tk.END, font_name)
        font_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        font_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # 現在のフォントを選択
        try:
            # 現在のフォント名を正規化して検索
            current_font = self.font_family_var.get()
            if current_font.startswith('@'):
                current_font = current_font[1:]
            
            # スタイルサフィックスを除去
            for suffix in style_suffixes:
                if current_font.endswith(suffix):
                    current_font = current_font[:-len(suffix)]
                    break
            
            # 完全一致または部分一致で検索
            current_index = 0
            for i, font_name in enumerate(unique_fonts):
                if font_name == current_font or current_font.startswith(font_name) or font_name.startswith(current_font):
                    current_index = i
                    break
            font_listbox.selection_set(current_index)
            font_listbox.see(current_index)
        except (ValueError, IndexError):
            pass
        
        # スタイル
        style_frame = ttk.LabelFrame(top_frame, text="スタイル(Y)", padding="5")
        style_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=False, padx=(0, 5))
        
        font_style_var = tk.StringVar(value=self.font_style_var.get())
        style_display_map = {
            'normal': '標準',
            'bold': '太字',
            'italic': '斜体',
            'bold italic': '太字 斜体'
        }
        style_values = ['normal', 'bold', 'italic', 'bold italic']
        # スクロールバーを作成
        style_scrollbar = tk.Scrollbar(style_frame, orient=tk.VERTICAL)
        style_listbox = tk.Listbox(style_frame, height=4, selectmode=tk.SINGLE, exportselection=False, yscrollcommand=style_scrollbar.set)
        style_scrollbar.config(command=style_listbox.yview)
        for style in style_values:
            style_listbox.insert(tk.END, style_display_map[style])
        style_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        style_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # 現在のスタイルを選択
        try:
            current_style_index = style_values.index(self.font_style_var.get())
            style_listbox.selection_set(current_style_index)
        except ValueError:
            style_listbox.selection_set(0)
        
        # サイズ
        size_frame = ttk.LabelFrame(top_frame, text="サイズ(S)", padding="5")
        size_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=False)
        
        font_size_var = tk.StringVar(value=self.font_size_var.get())
        size_values = ['8', '9', '10', '11', '12', '14', '16', '18', '20', '22', '24', '26', '28', '36', '48', '72']
        # スクロールバーを作成
        size_scrollbar = tk.Scrollbar(size_frame, orient=tk.VERTICAL)
        size_listbox = tk.Listbox(size_frame, height=4, selectmode=tk.SINGLE, exportselection=False, yscrollcommand=size_scrollbar.set)
        size_scrollbar.config(command=size_listbox.yview)
        for size in size_values:
            size_listbox.insert(tk.END, size)
        size_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        size_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # 現在のサイズを選択
        try:
            current_size_index = size_values.index(self.font_size_var.get())
            size_listbox.selection_set(current_size_index)
            size_listbox.see(current_size_index)
        except ValueError:
            size_listbox.selection_set(4)  # デフォルトで12を選択
        
        # サンプル（プレビュー）エリア
        sample_frame = ttk.LabelFrame(main_frame, text="サンプル", padding="10")
        sample_frame.pack(fill=tk.BOTH, expand=False, pady=(0, 10))
        
        preview_label = tk.Label(
            sample_frame,
            text="Aaあぁアァ亜宇",
            font=(font_family_var.get(), int(font_size_var.get()), font_style_var.get()),
            bg='white',
            relief=tk.SUNKEN,
            anchor='w',
            padx=10,
            pady=10
        )
        preview_label.pack(fill=tk.BOTH, expand=True)
        
        def update_preview():
            """プレビューを更新（選択は保持）"""
            try:
                # 現在の変数値を使用してプレビューを更新
                if font_family_var.get() and font_size_var.get() and font_style_var.get():
                    font_tuple = (font_family_var.get(), int(font_size_var.get()), font_style_var.get())
                    preview_label.config(font=font_tuple)
            except Exception as e:
                pass
        
        # リストボックスの選択変更イベント（各リストボックスで個別に処理）
        def on_font_select(event):
            """フォントファミリー選択時の処理"""
            selection = font_listbox.curselection()
            if selection:
                selected_family = unique_fonts[selection[0]]
                font_family_var.set(selected_family)
                update_preview()
            else:
                # 選択が外れた場合、変数の値に基づいて選択を再設定
                try:
                    current_font = font_family_var.get()
                    for i, font_name in enumerate(unique_fonts):
                        if font_name == current_font or current_font.startswith(font_name) or font_name.startswith(current_font):
                            font_listbox.selection_set(i)
                            font_listbox.see(i)
                            break
                except:
                    pass
        
        def on_style_select(event):
            """スタイル選択時の処理"""
            selection = style_listbox.curselection()
            if selection:
                selected_style = style_values[selection[0]]
                font_style_var.set(selected_style)
                update_preview()
            else:
                # 選択が外れた場合、変数の値に基づいて選択を再設定
                try:
                    current_style = font_style_var.get()
                    if current_style in style_values:
                        style_index = style_values.index(current_style)
                        style_listbox.selection_set(style_index)
                except:
                    pass
        
        def on_size_select(event):
            """サイズ選択時の処理"""
            selection = size_listbox.curselection()
            if selection:
                selected_size = size_values[selection[0]]
                font_size_var.set(selected_size)
                update_preview()
            else:
                # 選択が外れた場合、変数の値に基づいて選択を再設定
                try:
                    current_size = font_size_var.get()
                    if current_size in size_values:
                        size_index = size_values.index(current_size)
                        size_listbox.selection_set(size_index)
                        size_listbox.see(size_index)
                except:
                    pass
        
        # フォーカスが外れたときにも選択を保持
        def on_font_focus_out(event):
            """フォントリストボックスのフォーカスが外れたときの処理"""
            if not font_listbox.curselection():
                try:
                    current_font = font_family_var.get()
                    for i, font_name in enumerate(unique_fonts):
                        if font_name == current_font or current_font.startswith(font_name) or font_name.startswith(current_font):
                            font_listbox.selection_set(i)
                            font_listbox.see(i)
                            break
                except:
                    pass
        
        def on_style_focus_out(event):
            """スタイルリストボックスのフォーカスが外れたときの処理"""
            if not style_listbox.curselection():
                try:
                    current_style = font_style_var.get()
                    if current_style in style_values:
                        style_index = style_values.index(current_style)
                        style_listbox.selection_set(style_index)
                except:
                    pass
        
        def on_size_focus_out(event):
            """サイズリストボックスのフォーカスが外れたときの処理"""
            if not size_listbox.curselection():
                try:
                    current_size = font_size_var.get()
                    if current_size in size_values:
                        size_index = size_values.index(current_size)
                        size_listbox.selection_set(size_index)
                        size_listbox.see(size_index)
                except:
                    pass
        
        font_listbox.bind('<<ListboxSelect>>', on_font_select)
        font_listbox.bind('<FocusOut>', on_font_focus_out)
        style_listbox.bind('<<ListboxSelect>>', on_style_select)
        style_listbox.bind('<FocusOut>', on_style_focus_out)
        size_listbox.bind('<<ListboxSelect>>', on_size_select)
        size_listbox.bind('<FocusOut>', on_size_focus_out)
        
        # 初期プレビューを更新
        update_preview()
        
        # ボタンフレーム
        button_frame = ttk.Frame(main_frame)
        button_frame.pack(fill=tk.X)
        
        def apply_font():
            try:
                # 選択を取得
                font_selection = font_listbox.curselection()
                style_selection = style_listbox.curselection()
                size_selection = size_listbox.curselection()
                
                if not font_selection or not style_selection or not size_selection:
                    messagebox.showwarning("警告", "すべての項目を選択してください。")
                    return
                
                selected_family = unique_fonts[font_selection[0]]
                selected_style = style_values[style_selection[0]]
                selected_size = size_values[size_selection[0]]
                
                self.font_family_var.set(selected_family)
                self.font_size_var.set(selected_size)
                self.font_style_var.set(selected_style)
                
                # 表示を更新
                style_display = style_display_map.get(selected_style, selected_style)
                self.font_display_var.set(f"{selected_family}, {style_display}, {selected_size}pt")
                dialog.destroy()
            except Exception as e:
                messagebox.showerror("エラー", f"フォント設定に失敗しました: {e}")
        
        # ボタンを右側に配置
        ttk.Button(button_frame, text="キャンセル", command=dialog.destroy).pack(side=tk.RIGHT, padx=5)
        ttk.Button(button_frame, text="OK", command=apply_font).pack(side=tk.RIGHT, padx=5)
    
    def _save_settings(self):
        """設定を保存"""
        try:
            # ホットキーの検証
            hotkey = self.hotkey_var.get().strip().lower()
            if not hotkey:
                messagebox.showerror("エラー", "ホットキーを入力してください。")
                return
            
            # ホットキーの形式を簡単に検証（空でない、+が含まれているなど）
            if '+' not in hotkey and len(hotkey.split()) == 1:
                # 単一キーの場合は許可（例: f1, esc）
                pass
            elif '+' not in hotkey:
                messagebox.showerror(
                    "エラー",
                    "ホットキーの形式が正しくありません。\n"
                    "複数のキーを組み合わせる場合は「+」で区切ってください。\n"
                    "例: ctrl+shift+t"
                )
                return
            
            # フォントサイズの検証
            try:
                font_size = int(self.font_size_var.get().strip())
                if font_size < 8 or font_size > 72:
                    messagebox.showerror("エラー", "フォントサイズは8から72の間で指定してください。")
                    return
            except ValueError:
                messagebox.showerror("エラー", "フォントサイズは数値で指定してください。")
                return
            
            settings = {
                'GROQ_API_KEY': self.groq_api_key_var.get().strip(),
                'GROQ_MODEL': self.groq_model_var.get().strip(),
                'GOOGLE_API_KEY': self.google_api_key_var.get().strip(),
                'GOOGLE_APPLICATION_CREDENTIALS': self.google_creds_var.get().strip(),
                'OCR_METHOD': self.ocr_method_var.get().strip(),
                'HOTKEY': hotkey,
                'OVERLAY_FONT_FAMILY': self.font_family_var.get().strip(),
                'OVERLAY_FONT_SIZE': self.font_size_var.get().strip(),
                'OVERLAY_FONT_STYLE': self.font_style_var.get().strip(),
                'DEBUG_LOGGING': 'true' if self.debug_logging_var.get() else 'false',
            }
            
            # EasyOCRを選択した場合、モデルダウンロードの確認
            if settings['OCR_METHOD'] == 'easyocr':
                response = messagebox.askyesno(
                    "EasyOCRモデルのダウンロード",
                    "EasyOCRを選択しました。\n\n"
                    "初回使用時にモデルファイル（約500MB）をダウンロードします。\n"
                    "インターネット接続が必要です。\n\n"
                    "今すぐダウンロードしますか？\n"
                    "（「いいえ」を選択した場合、次回OCR使用時に自動的にダウンロードされます）"
                )
                
                if response:
                    # モデルダウンロードを実行
                    self._download_easyocr_models()
                else:
                    # 設定だけ保存（モデルは後でダウンロード）
                    self.settings_manager.save_all(settings)
                    messagebox.showinfo(
                        "設定",
                        "設定を保存しました。\n"
                        "EasyOCRのモデルは、次回OCR使用時に自動的にダウンロードされます。"
                    )
                    if self.on_save_callback:
                        self.on_save_callback()
                    self._close()
                    return
            
            # 設定を保存
            self.settings_manager.save_all(settings)
            
            messagebox.showinfo(
                "設定",
                "設定を保存しました。\n"
                "ホットキーの変更は即座に反映されます。\n"
                "一部の設定（OCR方法など）はアプリケーションの再起動が必要です。"
            )
            
            # コールバックを呼び出し
            if self.on_save_callback:
                self.on_save_callback()
            
            self._close()
            
        except Exception as e:
            messagebox.showerror("エラー", f"設定の保存に失敗しました:\n{str(e)}")
    
    def _download_easyocr_models(self):
        """EasyOCRのモデルをダウンロード"""
        # プログレスダイアログを作成
        progress_window = tk.Toplevel(self.root)
        progress_window.title("EasyOCRモデルダウンロード")
        progress_window.geometry("400x150")
        progress_window.resizable(False, False)
        progress_window.transient(self.root)
        progress_window.grab_set()
        
        # 中央に配置
        progress_window.update_idletasks()
        width = progress_window.winfo_width()
        height = progress_window.winfo_height()
        x = (progress_window.winfo_screenwidth() // 2) - (width // 2)
        y = (progress_window.winfo_screenheight() // 2) - (height // 2)
        progress_window.geometry(f'{width}x{height}+{x}+{y}')
        
        # ラベル
        label = ttk.Label(
            progress_window,
            text="EasyOCRのモデルファイルをダウンロード中...\n（約500MB、時間がかかる場合があります）",
            justify=tk.CENTER
        )
        label.pack(pady=20)
        
        # プログレスバー
        progress_var = tk.DoubleVar()
        progress_bar = ttk.Progressbar(
            progress_window,
            variable=progress_var,
            maximum=100,
            mode='indeterminate'
        )
        progress_bar.pack(pady=10, padx=20, fill=tk.X)
        progress_bar.start()
        
        # ステータスラベル
        status_label = ttk.Label(progress_window, text="準備中...")
        status_label.pack(pady=5)
        
        # キャンセルボタン（実際にはキャンセルできないが、UIとして表示）
        cancel_button = ttk.Button(progress_window, text="閉じる（ダウンロードは続行）", state=tk.DISABLED)
        cancel_button.pack(pady=10)
        
        # ダウンロードを別スレッドで実行
        def download_thread():
            try:
                status_label.config(text="EasyOCRをインポート中...")
                progress_window.update()
                
                import easyocr
                
                status_label.config(text="モデルファイルをダウンロード中...")
                progress_window.update()
                
                # EasyOCRのReaderを初期化（これによりモデルがダウンロードされる）
                reader = easyocr.Reader(['en', 'ja'], gpu=False)
                
                # ダウンロード完了
                progress_bar.stop()
                status_label.config(text="ダウンロード完了！")
                cancel_button.config(text="閉じる", state=tk.NORMAL, command=progress_window.destroy)
                
                # 設定を保存
                settings = {
                    'GROQ_API_KEY': self.groq_api_key_var.get().strip(),
                    'GROQ_MODEL': self.groq_model_var.get().strip(),
                    'GOOGLE_API_KEY': self.google_api_key_var.get().strip(),
                    'GOOGLE_APPLICATION_CREDENTIALS': self.google_creds_var.get().strip(),
                    'OCR_METHOD': self.ocr_method_var.get().strip(),
                    'OVERLAY_FONT_FAMILY': self.font_family_var.get().strip(),
                    'OVERLAY_FONT_SIZE': self.font_size_var.get().strip(),
                    'OVERLAY_FONT_STYLE': self.font_style_var.get().strip(),
                    'DEBUG_LOGGING': 'true' if self.debug_logging_var.get() else 'false',
                }
                self.settings_manager.save_all(settings)
                
                messagebox.showinfo("完了", "EasyOCRのモデルダウンロードが完了しました。\n設定を保存しました。")
                
                # コールバックを呼び出し
                if self.on_save_callback:
                    self.on_save_callback()
                
                # メインウィンドウを閉じる
                self.root.after(0, self._close)
                
            except ImportError:
                progress_bar.stop()
                status_label.config(text="エラー: EasyOCRがインストールされていません")
                cancel_button.config(text="閉じる", state=tk.NORMAL, command=progress_window.destroy)
                messagebox.showerror(
                    "エラー",
                    "EasyOCRがインストールされていません。\n"
                    "pip install easyocr でインストールしてください。"
                )
            except Exception as e:
                progress_bar.stop()
                status_label.config(text=f"エラー: {str(e)}")
                cancel_button.config(text="閉じる", state=tk.NORMAL, command=progress_window.destroy)
                messagebox.showerror("エラー", f"モデルのダウンロードに失敗しました:\n{str(e)}")
        
        # ダウンロードスレッドを開始
        download_thread_obj = threading.Thread(target=download_thread, daemon=True)
        download_thread_obj.start()
        
        # ウィンドウを閉じるボタンの動作を更新
        def on_closing():
            if cancel_button['state'] == tk.DISABLED:
                # ダウンロード中は閉じられない
                return
            progress_window.destroy()
        
        progress_window.protocol("WM_DELETE_WINDOW", on_closing)
    
    def _close(self):
        """ウィンドウを閉じる"""
        if self.root:
            # メインループを終了
            try:
                self.root.quit()
            except:
                pass
            
            # Tkinter変数の参照をクリア（destroy前に実行）
            for var in self._tk_vars:
                try:
                    var.set('')
                except:
                    pass
            self._tk_vars.clear()
            
            # ウィンドウを破棄
            try:
                self.root.destroy()
            except:
                pass
            self.root = None
    
    def show(self):
        """ウィンドウを表示（メインループを実行）"""
        if self.root:
            self.root.mainloop()
    
    def close(self):
        """ウィンドウを閉じる"""
        self._close()

