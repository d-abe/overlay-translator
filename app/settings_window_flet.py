"""
設定画面（Flet版）
"""
import os
import sys
import subprocess
from pathlib import Path

# 別プロセスで実行される場合、モジュールパスを修正
if __name__ == "__main__":
    # スクリプトの親ディレクトリをパスに追加
    script_dir = Path(__file__).parent.parent
    if str(script_dir) not in sys.path:
        sys.path.insert(0, str(script_dir))

import flet as ft
from app.settings import SettingsManager
from app.logger import warning, error, exception, debug

# 設定保存通知用のファイルパス
SETTINGS_SAVED_FLAG_FILE = Path(__file__).parent.parent / '.settings_saved'
# 設定ウィンドウのロックファイルパス
SETTINGS_LOCK_FILE = Path(__file__).parent.parent / '.settings_lock'
# 設定ウィンドウ終了シグナル用のファイルパス
SETTINGS_CLOSE_FLAG_FILE = Path(__file__).parent.parent / '.settings_close'

def run_flet_settings_window():
    """Flet設定ウィンドウを実行（独立したプロセスとして）"""
    import threading
    import time
    
    # デバッグ: どのスクリプトが実行されているかを確認
    debug(f"run_flet_settings_window()が実行されました")
    debug(f"__file__: {__file__}")
    debug(f"sys.argv: {sys.argv}")
    debug(f"sys.executable: {sys.executable}")
    if getattr(sys, 'frozen', False):
        debug(f"EXE化されたプロセスとして実行されています")
        if hasattr(sys, '_MEIPASS'):
            debug(f"_MEIPASS: {sys._MEIPASS}")
    else:
        debug(f"Pythonスクリプトとして実行されています")
    
    # EXE化された場合、_tcl_dataディレクトリを作成（PyInstallerのpyi_rth_tkinterフック用）
    if getattr(sys, 'frozen', False):
        if hasattr(sys, '_MEIPASS'):
            meipass_path = Path(sys._MEIPASS)
            tcl_data_dir = meipass_path / '_tcl_data'
            if not tcl_data_dir.exists():
                # Tclディレクトリを探す
                tcl_paths = [
                    meipass_path / '_internal' / 'tcl',
                    meipass_path / 'tcl',
                    meipass_path / 'tcl8' / '8.6',
                ]
                tcl_found = None
                for tcl_path in tcl_paths:
                    if tcl_path.exists():
                        tcl_found = tcl_path
                        break
                
                if tcl_found:
                    try:
                        # Windowsではシンボリックリンクの作成に管理者権限が必要な場合があるため、
                        # ディレクトリをコピーする
                        import shutil
                        shutil.copytree(tcl_found, tcl_data_dir, dirs_exist_ok=True)
                        debug(f"_tcl_dataディレクトリをコピー: {tcl_found} -> {tcl_data_dir}")
                    except Exception as e:
                        warning(f"_tcl_dataディレクトリの作成に失敗: {e}")
    
    # 親プロセスの監視（別スレッドで実行）
    def monitor_parent_process():
        """親プロセスが終了したら、このプロセスも終了する"""
        import os
        try:
            parent_pid = os.getppid()
            while True:
                time.sleep(0.5)  # 0.5秒ごとにチェック
                # 親プロセスが存在するか確認（Windows）
                if sys.platform == 'win32':
                    import ctypes
                    kernel32 = ctypes.windll.kernel32
                    handle = kernel32.OpenProcess(0x1000, False, parent_pid)  # PROCESS_QUERY_INFORMATION
                    if handle:
                        kernel32.CloseHandle(handle)
                    else:
                        # 親プロセスが存在しない
                        debug("親プロセスが終了したため、設定ウィンドウを終了します")
                        os._exit(0)
                else:
                    # Unix系の場合
                    try:
                        os.kill(parent_pid, 0)  # シグナル0はプロセスの存在確認のみ
                    except OSError:
                        # 親プロセスが存在しない
                        debug("親プロセスが終了したため、設定ウィンドウを終了します")
                        os._exit(0)
        except Exception as e:
            warning(f"親プロセス監視エラー: {e}")
            exception("親プロセス監視エラー", exc_info=True)
    
    # 親プロセス監視スレッドを開始
    monitor_thread = threading.Thread(target=monitor_parent_process, daemon=True)
    monitor_thread.start()
    
    def main(page: ft.Page):
        try:
            page.title = "設定 - Overlay Translator"
            page.window.width = 650
            page.window.height = 750
            page.window.min_width = 650
            page.window.min_height = 750
            page.window.resizable = False
            
            # 設定を読み込み
            settings_manager = SettingsManager()
            settings = settings_manager.get_all()
            
            # UIを構築
            _build_ui(page, settings_manager, settings)
            
            # ウィンドウを中央に配置
            page.window.center()
            
            # アイコンを設定（ウィンドウ表示後に設定）
            _set_window_icon(page)
            
            # 終了シグナルを監視（別スレッドで）
            import threading
            def watch_close_signal():
                """終了シグナルファイルを監視"""
                while True:
                    if SETTINGS_CLOSE_FLAG_FILE.exists():
                        debug("終了シグナルを検出しました")
                        try:
                            SETTINGS_CLOSE_FLAG_FILE.unlink()
                        except Exception:
                            pass
                        # ロックファイルを削除
                        if SETTINGS_LOCK_FILE.exists():
                            try:
                                SETTINGS_LOCK_FILE.unlink()
                            except Exception:
                                pass
                        # Flet 0.27.6のバグを回避する方法（GitHub Issue #5180の解決策）
                        # prevent_closeをFalseにしてからclose()を呼ぶ
                        try:
                            page.window.prevent_close = False
                            page.window.close()
                            debug("終了シグナル: ウィンドウを閉じました")
                        except Exception as e:
                            warning(f"ウィンドウの閉じる処理に失敗: {e}")
                            # フォールバック: プロセスを終了
                            import os
                            os._exit(0)
                        break
                    import time
                    time.sleep(0.1)  # 0.1秒ごとにチェック
            
            close_watch_thread = threading.Thread(target=watch_close_signal, daemon=True)
            close_watch_thread.start()
            
            # ウィンドウが閉じられたときの処理
            # 注意: Flet 0.27.6でpage.window.destroy()が遅い問題があるため、
            # prevent_closeは使わず、直接os._exit(0)でプロセスを終了する
            def window_event(e):
                if e.data == "close":
                    import time
                    event_start = time.time()
                    debug("window_event: closeイベントが発生しました")
                    # ロックファイルを削除（非同期で実行して遅延を防ぐ）
                    def cleanup():
                        cleanup_start = time.time()
                        if SETTINGS_LOCK_FILE.exists():
                            try:
                                SETTINGS_LOCK_FILE.unlink()
                                debug(f"window_event: ロックファイル削除完了: {time.time() - cleanup_start:.3f}秒")
                            except Exception as ex:
                                warning(f"ロックファイルの削除に失敗（ウィンドウクローズ時）: {ex}")
                    import threading
                    cleanup_thread = threading.Thread(target=cleanup, daemon=True)
                    cleanup_thread.start()
                    debug(f"window_event: クリーンアップスレッド開始: {time.time() - event_start:.3f}秒")
                    # Flet 0.27.6のバグを回避する方法（GitHub Issue #5180の解決策）
                    # prevent_closeをFalseにしてからclose()を呼ぶ
                    prevent_close_start = time.time()
                    page.window.prevent_close = False
                    debug(f"window_event: prevent_closeをFalseに設定: {time.time() - prevent_close_start:.3f}秒")
                    close_start = time.time()
                    page.window.close()
                    debug(f"window_event: page.window.close()完了: {time.time() - close_start:.3f}秒")
                    debug(f"window_event: 処理全体の時間: {time.time() - event_start:.3f}秒")
            
            # ウィンドウの自動クローズを防ぎ、イベントハンドラで処理
            # ただし、destroy()の代わりにos._exit(0)を使用
            page.window.prevent_close = True
            page.window.on_event = window_event
        except Exception as e:
            exception("Flet設定ウィンドウの初期化エラー", exc_info=True)
            # エラーが発生した場合でもウィンドウを表示
            page.add(ft.Text(f"エラーが発生しました: {str(e)}"))
    
    try:
        ft.app(target=main, view=ft.AppView.FLET_APP)
    except KeyboardInterrupt:
        # Ctrl+Cで終了された場合
        debug("Fletアプリがキーボード割り込みで終了しました")
    except Exception as e:
        exception("Fletアプリの起動エラー", exc_info=True)
    finally:
        # プロセスが終了するときにロックファイルを削除
        debug("Fletアプリが終了します（finallyブロック）")
        if SETTINGS_LOCK_FILE.exists():
            try:
                SETTINGS_LOCK_FILE.unlink()
                debug("ロックファイルを削除しました（finallyブロック）")
            except Exception as ex:
                warning(f"ロックファイルの削除に失敗（finallyブロック）: {ex}")

def _resource_path(relative_path):
    """PyInstallerでEXE化した場合も正しくリソースパスを取得する"""
    if hasattr(sys, '_MEIPASS'):
        # PyInstallerでEXE化された場合
        return os.path.join(sys._MEIPASS, relative_path)
    # 通常のPythonスクリプトとして実行されている場合
    base_path = Path(__file__).parent.parent
    return os.path.join(str(base_path), relative_path)

def _set_window_icon(page: ft.Page):
    """ウィンドウアイコンを設定（複数の方法を試す）"""
    try:
        # Windowsでは.icoファイルを優先、なければ.pngを試す
        icon_path = None
        
        # まず.icoファイルを試す
        ico_path = _resource_path("icon.ico")
        if os.path.exists(ico_path):
            icon_path = ico_path
        else:
            # .icoが見つからない場合は.pngを試す
            png_path = _resource_path("icon.png")
            if os.path.exists(png_path):
                icon_path = png_path
        
        if icon_path:
            # 絶対パスに変換
            icon_path_abs = os.path.abspath(icon_path)
            debug(f"アイコンを設定します: {icon_path_abs}")
            
            # 方法1: page.window.iconを設定
            try:
                page.window.icon = icon_path_abs
                debug("page.window.icon を設定しました")
            except Exception as e1:
                warning(f"page.window.icon の設定に失敗: {e1}")
            
            # 方法2: page.window.set_icon() が存在する場合（存在しない可能性が高い）
            try:
                if hasattr(page.window, 'set_icon'):
                    page.window.set_icon(icon_path_abs)
                    debug("page.window.set_icon() を呼び出しました")
            except Exception as e2:
                debug(f"page.window.set_icon() は利用できません: {e2}")
            
            # アイコン設定を反映するためにupdateを呼ぶ
            page.update()
            
            # 少し待ってから再度update（アイコンが遅れて反映される場合がある）
            import threading
            def delayed_update():
                import time
                time.sleep(0.5)
                try:
                    page.update()
                    debug("アイコン設定の遅延更新を実行しました")
                except:
                    pass
            threading.Thread(target=delayed_update, daemon=True).start()
        else:
            warning(f"アイコンファイルが見つかりません（icon.ico または icon.png）")
    except Exception as e:
        warning(f"アイコンの設定に失敗しました: {e}")
        exception("アイコン設定エラー", exc_info=True)

def _build_ui(page: ft.Page, settings_manager: SettingsManager, settings: dict):
    """UIを構築"""
    # スクロール可能なコンテナ
    scroll_view = ft.Column(
        scroll=ft.ScrollMode.AUTO,
        expand=True,
        spacing=10,
        controls=[]
    )
    
    # Groq API設定セクション
    groq_api_key_field = ft.TextField(
        label="APIキー",
        value=settings['GROQ_API_KEY'],
        password=True,
        can_reveal_password=True,
        expand=True,
    )
    
    groq_model_dropdown = ft.Dropdown(
        label="モデル",
        value=settings['GROQ_MODEL'],
        expand=True,
        options=[
            ft.dropdown.Option('llama-3.1-8b-instant', 'llama-3.1-8b-instant（高速・低コスト・推奨）'),
            ft.dropdown.Option('llama-3.3-70b-versatile', 'llama-3.3-70b-versatile（高品質）'),
            ft.dropdown.Option('openai/gpt-oss-120b', 'openai/gpt-oss-120b（最高品質）'),
            ft.dropdown.Option('openai/gpt-oss-20b', 'openai/gpt-oss-20b（高速）'),
            ft.dropdown.Option('meta-llama/llama-4-maverick-17b-128e-instruct', 'meta-llama/llama-4-maverick-17b-128e-instruct（Preview）'),
            ft.dropdown.Option('meta-llama/llama-4-scout-17b-16e-instruct', 'meta-llama/llama-4-scout-17b-16e-instruct（Preview）'),
            ft.dropdown.Option('qwen/qwen3-32b', 'qwen/qwen3-32b（Preview）'),
        ],
    )
    
    groq_section = ft.Container(
        content=ft.Column([
            ft.Text("Groq API設定（翻訳用）", weight=ft.FontWeight.BOLD, size=14),
            groq_api_key_field,
            groq_model_dropdown,
        ], spacing=10),
        padding=15,
        border=ft.border.all(1, "#9e9e9e"),
        border_radius=5,
        width=600,
    )
    
    # Google Cloud Vision API設定セクション
    google_api_key_field = ft.TextField(
        label="APIキー",
        value=settings['GOOGLE_API_KEY'],
        password=True,
        can_reveal_password=True,
        expand=True,
    )
    
    google_creds_field = ft.TextField(
        label="サービスアカウントJSONファイル",
        value=settings['GOOGLE_APPLICATION_CREDENTIALS'],
        expand=True,
        read_only=True,
    )
    
    def browse_json_file(e):
        file_picker = ft.FilePicker()
        page.overlay.append(file_picker)
        page.update()
        
        def on_result(e: ft.FilePickerResultEvent):
            if e.files and len(e.files) > 0:
                google_creds_field.value = e.files[0].path
                page.update()
            page.overlay.remove(file_picker)
            page.update()
        
        file_picker.on_result = on_result
        file_picker.pick_files(
            dialog_title="サービスアカウントJSONファイルを選択",
            allowed_extensions=["json"],
        )
    
    google_creds_row = ft.Row([
        google_creds_field,
        ft.ElevatedButton("参照...", on_click=browse_json_file, width=100),
    ], spacing=10, expand=True)
    
    google_section = ft.Container(
        content=ft.Column([
            ft.Text("Google Cloud Vision API設定（OCR用）", weight=ft.FontWeight.BOLD, size=14),
            google_api_key_field,
            google_creds_row,
        ], spacing=10),
        padding=15,
        border=ft.border.all(1, "#9e9e9e"),
        border_radius=5,
        width=600,
    )
    
    # OCR方法選択
    ocr_method_radio = ft.RadioGroup(
        content=ft.Column([
            ft.Radio(value="google", label="Google Cloud Vision API（高精度・請求が必要）"),
            ft.Radio(value="groq", label="Groq Vision API（高速・Groq APIキー使用）"),
            ft.Radio(value="easyocr", label="EasyOCR（オフライン・初回のみモデルダウンロード）"),
        ]),
        value=settings['OCR_METHOD'],
    )
    
    ocr_section = ft.Container(
        content=ft.Column([
            ft.Text("OCR方法", weight=ft.FontWeight.BOLD, size=14),
            ocr_method_radio,
        ], spacing=10),
        padding=15,
        border=ft.border.all(1, "#9e9e9e"),
        border_radius=5,
        width=600,
    )
    
    # ホットキー設定
    hotkey_field = ft.TextField(
        label="ホットキー",
        value=settings.get('HOTKEY', 'ctrl+shift+t'),
        expand=True,
        hint_text="例: ctrl+shift+t, alt+f1, ctrl+alt+s",
    )
    
    hotkey_help = ft.Text(
        "複数のキーは「+」で区切ります",
        size=10,
        color="#757575",
    )
    
    hotkey_section = ft.Container(
        content=ft.Column([
            ft.Text("ホットキー設定", weight=ft.FontWeight.BOLD, size=14),
            hotkey_field,
            hotkey_help,
        ], spacing=10),
        padding=15,
        border=ft.border.all(1, "#9e9e9e"),
        border_radius=5,
        width=600,
    )
    
    # オーバーレイ表示設定
    font_family = settings.get('OVERLAY_FONT_FAMILY', 'Meiryo')
    font_size = int(settings.get('OVERLAY_FONT_SIZE', '12'))
    font_style = settings.get('OVERLAY_FONT_STYLE', 'normal')
    
    style_display_map = {
        'normal': 'Regular',
        'bold': 'Bold',
        'italic': 'Italic',
        'bold italic': 'Bold Italic'
    }
    style_display = style_display_map.get(font_style, font_style)
    font_display_text = f"{font_family}, {style_display}, {font_size}pt"
    
    font_display_field = ft.TextField(
        label="フォント",
        value=font_display_text,
        expand=True,
        read_only=True,
    )
    
    # フォント情報を保持（内部用）- リストで保持して参照を共有
    font_family_value = [font_family]
    font_size_value = [str(font_size)]
    font_style_value = [font_style]
    
    def open_font_dialog(e):
        try:
            debug("フォント選択ボタンがクリックされました")
            def on_font_selected(family, size, style):
                font_family_value[0] = family
                font_size_value[0] = size
                font_style_value[0] = style
                style_display = style_display_map.get(style, style)
                font_display_field.value = f"{family}, {style_display}, {size}pt"
                page.update()
            debug("_show_font_dialogを呼び出します")
            _show_font_dialog(page, font_display_field, font_family_value[0], font_size_value[0], font_style_value[0], on_font_selected)
            debug("_show_font_dialogの呼び出し完了")
        except Exception as ex:
            exception("フォント選択ダイアログの開くエラー", exc_info=True)
    
    font_row = ft.Row([
        font_display_field,
        ft.ElevatedButton("フォントを選択...", on_click=open_font_dialog, width=150),
    ], spacing=10, expand=True)
    
    overlay_section = ft.Container(
        content=ft.Column([
            ft.Text("オーバーレイ表示設定", weight=ft.FontWeight.BOLD, size=14),
            font_row,
        ], spacing=10),
        padding=15,
        border=ft.border.all(1, "#9e9e9e"),
        border_radius=5,
        width=600,
    )
    
    # デバッグ設定
    debug_checkbox = ft.Checkbox(
        label="DEBUGログを有効にする",
        value=settings.get('DEBUG_LOGGING', 'false').lower() in ('true', '1', 'yes', 'on'),
    )
    
    debug_section = ft.Container(
        content=ft.Column([
            ft.Text("デバッグ設定", weight=ft.FontWeight.BOLD, size=14),
            debug_checkbox,
        ], spacing=10),
        padding=15,
        border=ft.border.all(1, "#9e9e9e"),
        border_radius=5,
        width=600,
    )
    
    # 保存ボタン
    def save_settings(e):
        try:
            # フォント情報を更新（ダイアログから変更された可能性があるため）
            # 注意: この実装では、フォントダイアログで変更された値が反映されない可能性がある
            # より良い実装では、フォントダイアログから値を返す必要がある
            
            # ホットキーの検証
            hotkey = hotkey_field.value.strip().lower()
            if not hotkey:
                _show_error(page, "ホットキーを入力してください。")
                return
            
            # ホットキーの形式を簡単に検証
            if '+' not in hotkey and len(hotkey.split()) == 1:
                pass  # 単一キーの場合は許可
            elif '+' not in hotkey:
                _show_error(
                    page,
                    "ホットキーの形式が正しくありません。\n"
                    "複数のキーを組み合わせる場合は「+」で区切ってください。\n"
                    "例: ctrl+shift+t"
                )
                return
            
            # フォントサイズの検証
            try:
                font_size_int = int(font_size_value[0])
                if font_size_int < 8 or font_size_int > 72:
                    _show_error(page, "フォントサイズは8から72の間で指定してください。")
                    return
            except ValueError:
                _show_error(page, "フォントサイズは数値で指定してください。")
                return
            
            settings_dict = {
                'GROQ_API_KEY': groq_api_key_field.value.strip(),
                'GROQ_MODEL': groq_model_dropdown.value.strip(),
                'GOOGLE_API_KEY': google_api_key_field.value.strip(),
                'GOOGLE_APPLICATION_CREDENTIALS': google_creds_field.value.strip(),
                'OCR_METHOD': ocr_method_radio.value.strip(),
                'HOTKEY': hotkey,
                'OVERLAY_FONT_FAMILY': font_family_value[0].strip(),
                'OVERLAY_FONT_SIZE': font_size_value[0].strip(),
                'OVERLAY_FONT_STYLE': font_style_value[0].strip(),
                'DEBUG_LOGGING': 'true' if debug_checkbox.value else 'false',
            }
            
            # EasyOCRを選択した場合、モデルダウンロードの確認
            if settings_dict['OCR_METHOD'] == 'easyocr':
                dialog = ft.AlertDialog(
                    modal=True,
                    title=ft.Text("確認"),
                    content=ft.Text("EasyOCRのモデルファイルをダウンロードしますか？\n（約500MB、時間がかかる場合があります）"),
                    actions=[
                        ft.TextButton("はい", on_click=on_yes),
                        ft.TextButton("いいえ", on_click=on_no),
                    ],
                )
                
                def on_yes(e):
                    page.close(dialog)
                    _download_easyocr_models(page, settings_manager, settings_dict)
                
                def on_no(e):
                    page.close(dialog)
                    # 設定だけ保存
                    settings_manager.save_all(settings_dict)
                    # 設定保存通知ファイルを作成
                    SETTINGS_SAVED_FLAG_FILE.touch()
                    _show_info(page, "設定を保存しました。\nEasyOCRのモデルは、次回OCR使用時に自動的にダウンロードされます。")
                    # 注意: _show_info内でOKボタンを押したときにウィンドウを閉じる
                
                page.open(dialog)
                return
            
            # EasyOCR以外の場合、またはEasyOCRで「いいえ」を選択した場合
            # 設定を保存
            settings_manager.save_all(settings_dict)
            # 設定保存通知ファイルを作成
            SETTINGS_SAVED_FLAG_FILE.touch()
            _show_info(page, "設定を保存しました。")
            # 注意: _show_info内でOKボタンを押したときにウィンドウを閉じる
        except Exception as e:
            _show_error(page, f"設定の保存に失敗しました:\n{str(e)}")
    
    def cancel_settings(e):
        import time
        start_time = time.time()
        debug("キャンセルボタンが押されました")
        
        # ロックファイルを削除（非同期で実行して遅延を防ぐ）
        def cleanup():
            cleanup_start = time.time()
            if SETTINGS_LOCK_FILE.exists():
                try:
                    SETTINGS_LOCK_FILE.unlink()
                    debug(f"ロックファイル削除完了: {time.time() - cleanup_start:.3f}秒")
                except Exception as ex:
                    warning(f"ロックファイルの削除に失敗: {ex}")
        import threading
        cleanup_thread = threading.Thread(target=cleanup, daemon=True)
        cleanup_thread.start()
        debug(f"クリーンアップスレッド開始: {time.time() - start_time:.3f}秒")
        
        # Flet 0.27.6のバグを回避する方法（GitHub Issue #5180の解決策）
        # prevent_closeをFalseにしてからclose()を呼ぶ
        try:
            prevent_close_start = time.time()
            page.window.prevent_close = False
            debug(f"prevent_closeをFalseに設定: {time.time() - prevent_close_start:.3f}秒")
            close_start = time.time()
            page.window.close()
            debug(f"page.window.close()完了: {time.time() - close_start:.3f}秒")
            debug(f"キャンセル処理全体の時間: {time.time() - start_time:.3f}秒")
        except Exception as ex:
            warning(f"ウィンドウの閉じる処理に失敗: {ex}")
            exception("ウィンドウ閉じるエラー", exc_info=True)
    
    button_row = ft.Row([
        ft.ElevatedButton("キャンセル", on_click=cancel_settings, width=100),
        ft.ElevatedButton("保存", on_click=save_settings, width=100),
    ], alignment=ft.MainAxisAlignment.END, spacing=10)
    
    # すべてのセクションを追加
    scroll_view.controls.extend([
        groq_section,
        google_section,
        ocr_section,
        hotkey_section,
        overlay_section,
        debug_section,
        button_row,
    ])
    
    page.add(scroll_view)

def _show_font_dialog(page: ft.Page, font_display_field: ft.TextField, current_family: str, current_size: str, current_style: str, on_selected=None):
    """フォント選択ダイアログを表示"""
    try:
        debug("_show_font_dialogが呼ばれました")
        # フォントファミリーのリストを取得（tkinterを使用）
        unique_fonts = []
        try:
            import tkinter as tk
            import tkinter.font as tkfont
            root = tk.Tk()
            root.withdraw()  # ウィンドウを表示しない
            all_fonts = tkfont.families()
            seen = set()
            for font_name in sorted(all_fonts):
                if font_name.startswith('@'):
                    continue
                # スタイルサフィックスを除去してベース名を取得
                base_name = font_name
                style_suffixes = [' Bold', ' Italic', ' Regular', ' Normal', ' Light', ' Medium', ' Heavy', ' Black']
                for suffix in style_suffixes:
                    if base_name.endswith(suffix):
                        base_name = base_name[:-len(suffix)]
                        break
                if base_name and base_name not in seen:
                    seen.add(base_name)
                    unique_fonts.append(base_name)
            root.destroy()
            debug(f"tkinterから{len(unique_fonts)}個のフォントを取得しました")
        except Exception as e:
            warning(f"フォントリストの取得に失敗: {e}")
            exception("フォントリストの取得エラー", exc_info=True)
            # フォールバック: 基本的なフォントリスト
            unique_fonts = ['Arial', 'Meiryo', 'MS Gothic', 'MS PGothic', 'Yu Gothic', 'Times New Roman', 'Courier New']
        
        # ドロップダウンを作成
        font_family_dropdown = ft.Dropdown(
            label="フォント名",
            value=current_family if current_family in unique_fonts else unique_fonts[0] if unique_fonts else 'Arial',
            options=[ft.dropdown.Option(f) for f in unique_fonts],
            width=200,
        )
        
        style_display_map = {
            'normal': 'Regular',
            'bold': 'Bold',
            'italic': 'Italic',
            'bold italic': 'Bold Italic'
        }
        style_values = ['normal', 'bold', 'italic', 'bold italic']
        
        font_style_dropdown = ft.Dropdown(
            label="スタイル",
            value=current_style if current_style in style_values else 'normal',
            options=[
                ft.dropdown.Option('normal', 'Regular'),
                ft.dropdown.Option('bold', 'Bold'),
                ft.dropdown.Option('italic', 'Italic'),
                ft.dropdown.Option('bold italic', 'Bold Italic'),
            ],
            width=150,
        )
        
        size_values = ['8', '9', '10', '11', '12', '14', '16', '18', '20', '22', '24', '26', '28', '36', '48', '72']
        font_size_dropdown = ft.Dropdown(
            label="サイズ",
            value=current_size if current_size in size_values else '12',
            options=[ft.dropdown.Option(s) for s in size_values],
            width=100,
        )
        
        # プレビュー
        font_preview = ft.Text(
            value="Aaあぁアァ亜宇",
            size=int(current_size),
            weight=ft.FontWeight.BOLD if 'bold' in current_style else ft.FontWeight.NORMAL,
            italic='italic' in current_style,
            font_family=current_family,
        )
        
        def update_preview():
            family = font_family_dropdown.value
            style = font_style_dropdown.value
            size = font_size_dropdown.value
            
            # フォントスタイルをFletの形式に変換
            weight = ft.FontWeight.BOLD if 'bold' in style else ft.FontWeight.NORMAL
            italic = 'italic' in style
            
            font_preview.value = "Aaあぁアァ亜宇"
            font_preview.size = int(size)
            font_preview.weight = weight
            font_preview.italic = italic
            font_preview.font_family = family
            page.update()
        
        # ドロップダウンの変更イベント
        font_family_dropdown.on_change = lambda e: update_preview()
        font_style_dropdown.on_change = lambda e: update_preview()
        font_size_dropdown.on_change = lambda e: update_preview()
        
        # 初期プレビューを更新
        update_preview()
        
        # フォント情報を保持する変数（クロージャで使用）
        selected_family = [current_family]
        selected_size = [current_size]
        selected_style = [current_style]
        
        def apply_font(e):
            selected_family[0] = font_family_dropdown.value
            selected_size[0] = font_size_dropdown.value
            selected_style[0] = font_style_dropdown.value
            
            page.close(dialog)
            
            # コールバックを呼び出し
            if on_selected:
                on_selected(selected_family[0], selected_size[0], selected_style[0])
        
        def cancel_font(e):
            page.close(dialog)
        
        # ダイアログのコンテンツを作成
        dialog_content = ft.Column([
            # 1行目：フォント選択
            font_family_dropdown,
            # 2行目：スタイルとサイズの選択
            ft.Row([
                font_style_dropdown,
                font_size_dropdown,
            ], spacing=10),
            # プレビュー表示（システム背景色・システム文字色を使用）
            ft.Container(
                content=font_preview,
                padding=10,
                border_radius=5,
                width=500,
                height=60,
                alignment=ft.alignment.center,
            ),
        ], spacing=10, tight=True)
        
        dialog = ft.AlertDialog(
            modal=True,
            title=ft.Text("フォント"),
            content=dialog_content,
            actions=[
                ft.TextButton("キャンセル", on_click=cancel_font),
                ft.TextButton("OK", on_click=apply_font),
            ],
        )
        
        debug("ダイアログを表示します")
        # Fletのダイアログ表示方法（page.openを使用）
        page.open(dialog)
        debug("ダイアログ表示完了")
    except Exception as e:
        exception("フォント選択ダイアログの表示エラー", exc_info=True)
        # エラーダイアログを表示
        error_dialog = ft.AlertDialog(
            modal=True,
            title=ft.Text("エラー"),
            content=ft.Text(f"フォント選択ダイアログの表示に失敗しました:\n{str(e)}"),
            actions=[ft.TextButton("OK", on_click=lambda e: page.close(error_dialog))],
        )
        page.open(error_dialog)

def _download_easyocr_models(page: ft.Page, settings_manager: SettingsManager, settings: dict):
    """EasyOCRのモデルをダウンロード"""
    # プログレスダイアログ
    progress_text = ft.Text("準備中...")
    progress_bar = ft.ProgressBar(width=400)
    
    def close_progress(e):
        page.close(progress_dialog)
    
    progress_dialog = ft.AlertDialog(
        modal=True,
        title=ft.Text("EasyOCRモデルダウンロード"),
        content=ft.Container(
            content=ft.Column([
                ft.Text("EasyOCRのモデルファイルをダウンロード中...\n（約500MB、時間がかかる場合があります）"),
                progress_bar,
                progress_text,
            ], spacing=10, tight=True),
            width=450,
            height=150,
        ),
        actions=[
            ft.TextButton("閉じる（ダウンロードは続行）", on_click=close_progress, disabled=True),
        ],
    )
    
    page.open(progress_dialog)
    
    def download_thread():
        try:
            import threading as th
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_text, 'value', 'EasyOCRをインポート中...'))).start()
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_bar, 'value', None))).start()  # インデターミネートモード
            th.Thread(target=lambda: page.run_task(page.update)).start()
            
            import easyocr
            
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_text, 'value', 'モデルファイルをダウンロード中...'))).start()
            th.Thread(target=lambda: page.run_task(page.update)).start()
            
            # EasyOCRのReaderを初期化（これによりモデルがダウンロードされる）
            reader = easyocr.Reader(['en', 'ja'], gpu=False)
            
            # ダウンロード完了
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_bar, 'value', 1.0))).start()
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_text, 'value', 'ダウンロード完了！'))).start()
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_dialog.actions[0], 'disabled', False))).start()
            th.Thread(target=lambda: page.run_task(page.update)).start()
            
            # 設定を保存
            settings_manager.save_all(settings)
            # 設定保存通知ファイルを作成
            SETTINGS_SAVED_FLAG_FILE.touch()
            
            th.Thread(target=lambda: page.run_task(lambda: _show_info(page, "EasyOCRのモデルダウンロードが完了しました。\n設定を保存しました。"))).start()
            
            # メインウィンドウを閉じる
            th.Thread(target=lambda: page.run_task(lambda: setattr(page.window, 'close', True))).start()
            th.Thread(target=lambda: page.run_task(page.update)).start()
            
        except ImportError:
            import threading as th
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_bar, 'value', 1.0))).start()
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_text, 'value', 'エラー: EasyOCRがインストールされていません'))).start()
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_dialog.actions[0], 'disabled', False))).start()
            th.Thread(target=lambda: page.run_task(page.update)).start()
            th.Thread(target=lambda: page.run_task(lambda: _show_error(page, "EasyOCRがインストールされていません。\npip install easyocr でインストールしてください。"))).start()
        except Exception as ex:
            import threading as th
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_bar, 'value', 1.0))).start()
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_text, 'value', f'エラー: {str(ex)}'))).start()
            th.Thread(target=lambda: page.run_task(lambda: setattr(progress_dialog.actions[0], 'disabled', False))).start()
            th.Thread(target=lambda: page.run_task(page.update)).start()
            th.Thread(target=lambda: page.run_task(lambda: _show_error(page, f"モデルのダウンロードに失敗しました:\n{str(ex)}"))).start()
    
    # ダウンロードスレッドを開始
    import threading
    download_thread_obj = threading.Thread(target=download_thread, daemon=True)
    download_thread_obj.start()

def _show_error(page: ft.Page, message: str):
    """エラーダイアログを表示"""
    dialog = ft.AlertDialog(
        modal=True,
        title=ft.Text("エラー"),
        content=ft.Text(message),
        actions=[ft.TextButton("OK", on_click=lambda e: page.close(dialog))],
    )
    page.open(dialog)

def _show_info(page: ft.Page, message: str):
    """情報ダイアログを表示"""
    def on_ok(e):
        import time
        ok_start = time.time()
        debug("_show_info: OKボタンが押されました")
        
        close_start = time.time()
        page.close(dialog)
        debug(f"_show_info: page.close(dialog)完了: {time.time() - close_start:.3f}秒")
        
        # ロックファイルを削除（非同期で実行して遅延を防ぐ）
        def cleanup():
            cleanup_start = time.time()
            if SETTINGS_LOCK_FILE.exists():
                try:
                    SETTINGS_LOCK_FILE.unlink()
                    debug(f"_show_info: ロックファイル削除完了: {time.time() - cleanup_start:.3f}秒")
                except Exception as ex:
                    warning(f"ロックファイルの削除に失敗: {ex}")
        import threading
        cleanup_thread = threading.Thread(target=cleanup, daemon=True)
        cleanup_thread.start()
        debug(f"_show_info: クリーンアップスレッド開始: {time.time() - ok_start:.3f}秒")
        
        # Flet 0.27.6のバグを回避する方法（GitHub Issue #5180の解決策）
        # prevent_closeをFalseにしてからclose()を呼ぶ
        try:
            prevent_close_start = time.time()
            page.window.prevent_close = False
            debug(f"_show_info: prevent_closeをFalseに設定: {time.time() - prevent_close_start:.3f}秒")
            close_window_start = time.time()
            page.window.close()
            debug(f"_show_info: page.window.close()完了: {time.time() - close_window_start:.3f}秒")
            debug(f"_show_info: 処理全体の時間: {time.time() - ok_start:.3f}秒")
        except Exception as ex:
            warning(f"ウィンドウの閉じる処理に失敗: {ex}")
            exception("ウィンドウ閉じるエラー", exc_info=True)
    
    dialog = ft.AlertDialog(
        modal=True,
        title=ft.Text("情報"),
        content=ft.Text(message),
        actions=[ft.TextButton("OK", on_click=on_ok)],
    )
    page.open(dialog)

class SettingsWindowFlet:
    """Flet設定ウィンドウのラッパークラス"""
    def __init__(self, on_save_callback=None):
        self.on_save_callback = on_save_callback
        self._process = None
    
    def show(self):
        """設定ウィンドウを表示"""
        # EXE化された場合でも別プロセスで実行するが、
        # PyInstallerの一時ディレクトリの問題を解決するために
        # 環境変数を適切に設定する
        
        # Pythonスクリプトとして実行されている場合：別プロセスで実行
        # 既存のプロセスが実行中の場合はスキップ
        if self._process and self._process.poll() is None:
            return
        
        # ロックファイルをチェック（他のプロセスが実行中かどうか）
        # 起動時に古いロックファイルをクリーンアップ
        if SETTINGS_LOCK_FILE.exists():
            # ロックファイルが存在する場合、プロセスIDを確認
            # ファイルがロックされている可能性があるため、リトライロジックを追加
            import time
            lock_pid_str = None
            for _ in range(3):  # 最大3回リトライ
                try:
                    with open(SETTINGS_LOCK_FILE, 'r', encoding='utf-8') as f:
                        lock_pid_str = f.read().strip()
                    break
                except (PermissionError, OSError):
                    time.sleep(0.1)  # 0.1秒待機してリトライ
            
            if lock_pid_str is None:
                # ファイルが読めない場合は削除を試みる
                try:
                    SETTINGS_LOCK_FILE.unlink()
                except Exception:
                    pass
            elif not lock_pid_str:
                # 空のファイルなので削除
                try:
                    SETTINGS_LOCK_FILE.unlink()
                except Exception:
                    pass
            else:
                try:
                    lock_pid = int(lock_pid_str)
                    # プロセスが実行中かどうかを確認（Windows）
                    if sys.platform == 'win32':
                        import ctypes
                        kernel32 = ctypes.windll.kernel32
                        handle = kernel32.OpenProcess(0x1000, False, lock_pid)  # PROCESS_QUERY_INFORMATION
                        if handle:
                            kernel32.CloseHandle(handle)
                            # プロセスが実行中なので、スキップ
                            return
                        else:
                            # プロセスが存在しないので、ロックファイルを削除
                            try:
                                SETTINGS_LOCK_FILE.unlink()
                            except Exception:
                                pass
                    else:
                        # Unix系の場合
                        try:
                            os.kill(lock_pid, 0)  # シグナル0はプロセスの存在確認のみ
                            # プロセスが実行中なので、スキップ
                            return
                        except OSError:
                            # プロセスが存在しないので、ロックファイルを削除
                            try:
                                SETTINGS_LOCK_FILE.unlink()
                            except Exception:
                                pass
                except (ValueError, FileNotFoundError) as e:
                    # ロックファイルが無効なので削除
                    warning(f"ロックファイルの読み込みエラー: {e}")
                    try:
                        if SETTINGS_LOCK_FILE.exists():
                            SETTINGS_LOCK_FILE.unlink()
                    except Exception:
                        pass
                except Exception as e:
                    # その他のエラーもロックファイルを削除
                    warning(f"ロックファイルのチェックエラー: {e}")
                    try:
                        if SETTINGS_LOCK_FILE.exists():
                            SETTINGS_LOCK_FILE.unlink()
                    except Exception:
                        pass
        
        # 設定保存通知ファイルを削除
        if SETTINGS_SAVED_FLAG_FILE.exists():
            SETTINGS_SAVED_FLAG_FILE.unlink()
        
        # 別プロセスでFletアプリを実行
        python_exe = sys.executable
        
        # デバッグ: 起動するスクリプトとコマンドを確認
        debug(f"設定ウィンドウプロセスを起動します")
        debug(f"実行ファイル: {python_exe}")
        
        # EXE化された場合、EXEファイル自体を実行してコマンドライン引数でモードを指定
        # これにより、一時ディレクトリ内のパスの問題を回避
        if getattr(sys, 'frozen', False):
            # EXE化された場合、EXEファイル自体を実行
            script_path = python_exe
            cmd = [python_exe, '--settings']
            debug(f"EXE化されたプロセス: EXEファイル自体を実行します")
        else:
            # Pythonスクリプトとして実行されている場合
            script_path = Path(__file__)
            cmd = [python_exe, str(script_path)]
            debug(f"Pythonスクリプト: {script_path}")
        
        # ターミナルを開かないようにする（Windows）
        creation_flags = 0
        if sys.platform == 'win32':
            # CREATE_NO_WINDOWフラグを使用してコンソールウィンドウを開かない
            creation_flags = subprocess.CREATE_NO_WINDOW
        
        try:
            # エラーをログファイルに出力するために、stderrをファイルにリダイレクト
            log_file = Path(__file__).parent.parent / 'flet_settings_error.log'
            log_handle = open(log_file, 'w', encoding='utf-8')
            # 環境変数を設定（PYTHONPATHにプロジェクトルートを追加）
            env = os.environ.copy()
            
            # プロジェクトルートを取得（EXE化された場合とそうでない場合で異なる）
            if getattr(sys, 'frozen', False):
                # EXE化された場合、実行ファイルのディレクトリをプロジェクトルートとする
                project_root = Path(python_exe).parent
            else:
                project_root = Path(__file__).parent.parent
            
            # デバッグ: 環境変数を確認
            debug(f"プロジェクトルート: {project_root}")
            if getattr(sys, 'frozen', False):
                debug(f"EXE化されたプロセスから起動します")
                if hasattr(sys, '_MEIPASS'):
                    # PyInstallerの一時ディレクトリを環境変数に設定
                    env['_MEIPASS'] = sys._MEIPASS
                    # Tcl/Tkのデータディレクトリを設定（複数のパスを試す）
                    meipass_path = Path(sys._MEIPASS)
                    # PyInstallerのonefileモードでは、tcl/tkは通常_internalディレクトリに配置される
                    tcl_paths = [
                        meipass_path / '_internal' / 'tcl',
                        meipass_path / 'tcl',
                        meipass_path / 'tcl8' / '8.6',
                    ]
                    tk_paths = [
                        meipass_path / '_internal' / 'tk',
                        meipass_path / 'tk',
                        meipass_path / 'tk8' / '8.6',
                    ]
                    tcl_found = None
                    tk_found = None
                    for tcl_path in tcl_paths:
                        if tcl_path.exists():
                            env['TCL_LIBRARY'] = str(tcl_path)
                            tcl_found = tcl_path
                            debug(f"TCL_LIBRARYを設定: {tcl_path}")
                            break
                    for tk_path in tk_paths:
                        if tk_path.exists():
                            env['TK_LIBRARY'] = str(tk_path)
                            tk_found = tk_path
                            debug(f"TK_LIBRARYを設定: {tk_path}")
                            break
            else:
                debug(f"Pythonスクリプトから起動します")
            
            if 'PYTHONPATH' in env:
                env['PYTHONPATH'] = str(project_root) + os.pathsep + env['PYTHONPATH']
            else:
                env['PYTHONPATH'] = str(project_root)
            
            # 起動コマンドを構築
            debug(f"起動コマンド: {cmd}")
            debug(f"作業ディレクトリ: {project_root}")
            debug(f"環境変数 _MEIPASS: {env.get('_MEIPASS', '未設定')}")
            debug(f"環境変数 TCL_LIBRARY: {env.get('TCL_LIBRARY', '未設定')}")
            debug(f"環境変数 TK_LIBRARY: {env.get('TK_LIBRARY', '未設定')}")
            
            self._process = subprocess.Popen(
                cmd,
                creationflags=creation_flags,
                stdout=subprocess.DEVNULL,
                stderr=log_handle,  # エラーをログファイルに出力
                stdin=subprocess.DEVNULL,
                env=env,  # 環境変数を設定
                cwd=str(project_root)  # 作業ディレクトリをプロジェクトルートに設定
            )
            
            debug(f"設定ウィンドウプロセスを起動しました (PID: {self._process.pid})")
            
            # ロックファイルを作成（起動したプロセスのIDを書き込む）
            try:
                with open(SETTINGS_LOCK_FILE, 'w', encoding='utf-8') as f:
                    f.write(str(self._process.pid))
            except Exception as e:
                warning(f"ロックファイルの作成に失敗: {e}")
            
            # プロセスが終了したらファイルを閉じる（別スレッドで）
            import threading
            def close_log_when_done():
                self._process.wait()
                log_handle.close()
                # プロセスが終了したらロックファイルを削除
                if SETTINGS_LOCK_FILE.exists():
                    try:
                        SETTINGS_LOCK_FILE.unlink()
                    except Exception:
                        pass
            threading.Thread(target=close_log_when_done, daemon=True).start()
        except Exception as e:
            error(f"Flet設定ウィンドウの起動に失敗しました: {e}")
            exception("Flet設定ウィンドウの起動エラー", exc_info=True)
            # エラーが発生した場合、ロックファイルを削除
            if SETTINGS_LOCK_FILE.exists():
                try:
                    SETTINGS_LOCK_FILE.unlink()
                except Exception:
                    pass
        
        # 設定保存通知ファイルを監視（別スレッドで）
        import threading
        def watch_settings_saved():
            while self._process and self._process.poll() is None:
                if SETTINGS_SAVED_FLAG_FILE.exists():
                    # 設定が保存された
                    SETTINGS_SAVED_FLAG_FILE.unlink()
                    if self.on_save_callback:
                        self.on_save_callback()
                    break
                import time
                time.sleep(0.5)
        
        watch_thread = threading.Thread(target=watch_settings_saved, daemon=True)
        watch_thread.start()
    
    def close(self):
        """ウィンドウを閉じる"""
        debug("SettingsWindowFlet.close()が呼ばれました")
        if self._process and self._process.poll() is None:
            debug("終了シグナルファイルを作成します...")
            try:
                # 終了シグナルファイルを作成（Fletアプリケーション側で監視している）
                SETTINGS_CLOSE_FLAG_FILE.touch()
                debug("終了シグナルファイルを作成しました")
                # プロセスの終了を待つ（最大5秒）
                import time
                for _ in range(50):  # 0.1秒 × 50 = 5秒
                    if self._process.poll() is not None:
                        debug("プロセスが正常に終了しました")
                        break
                    time.sleep(0.1)
                # まだ実行中の場合は強制終了（フォールバック）
                if self._process.poll() is None:
                    warning("プロセスが終了しないため、強制終了します...")
                    try:
                        self._process.terminate()
                        time.sleep(1.0)
                        if self._process.poll() is None:
                            self._process.kill()
                            self._process.wait(timeout=2)
                    except Exception as kill_error:
                        warning(f"プロセスの強制終了に失敗: {kill_error}")
            except Exception as e:
                warning(f"設定ウィンドウの終了シグナル送信に失敗: {e}")
                exception("設定ウィンドウ終了シグナル送信エラー", exc_info=True)
                # エラーが発生した場合も強制終了を試みる
                if self._process and self._process.poll() is None:
                    try:
                        self._process.terminate()
                        time.sleep(0.5)
                        if self._process.poll() is None:
                            self._process.kill()
                    except Exception:
                        pass
        # ロックファイルを削除
        if SETTINGS_LOCK_FILE.exists():
            try:
                SETTINGS_LOCK_FILE.unlink()
                debug("ロックファイルを削除しました")
            except Exception as e:
                warning(f"ロックファイルの削除に失敗: {e}")
        # 終了シグナルファイルも削除（念のため）
        if SETTINGS_CLOSE_FLAG_FILE.exists():
            try:
                SETTINGS_CLOSE_FLAG_FILE.unlink()
            except Exception:
                pass

if __name__ == "__main__":
    # 独立したスクリプトとして実行された場合
    # デバッグ: このスクリプトが直接実行されたことを確認
    # 注意: ロガーは別プロセスで実行されるため、ログファイルのパスが異なる可能性がある
    try:
        debug(f"settings_window_flet.pyが直接実行されました")
        debug(f"__name__: {__name__}")
        debug(f"__file__: {__file__}")
        debug(f"sys.argv: {sys.argv}")
        debug(f"sys.executable: {sys.executable}")
        
        # main.pyが実行されていないことを確認
        import inspect
        frame = inspect.currentframe()
        if frame:
            caller_frame = frame.f_back
            if caller_frame:
                caller_file = caller_frame.f_globals.get('__file__', '不明')
                debug(f"呼び出し元ファイル: {caller_file}")
    except Exception as e:
        # ロガーの初期化に失敗した場合でも続行
        import traceback
        traceback.print_exc()
    
    # main.pyが実行されていないことを確認（sys.argvにmain.pyが含まれていない）
    if 'main.py' in str(sys.argv):
        error("警告: main.pyが実行されている可能性があります")
        import sys
        sys.exit(1)
    
    run_flet_settings_window()
