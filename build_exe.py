"""
PyInstallerを使用してexeファイルをビルドするスクリプト
"""
import PyInstaller.__main__
import os
import sys

def build():
    """exeファイルをビルド"""
    
    # PyInstallerのオプション
    args = [
        'main.py',                    # メインファイル
        '--name=OverlayTranslator',   # exeファイル名
        '--onefile',                  # 単一ファイルにパッケージ化
        '--windowed',                 # コンソールウィンドウを非表示（GUIアプリ）
        '--noconsole',                # コンソールなし（--windowedと同じ）
        '--clean',                    # ビルド前に一時ファイルをクリーンアップ
        
        # 隠しインポート（PyInstallerが自動検出できないモジュール）
        '--hidden-import=pystray',
        '--hidden-import=pystray._win32',
        '--hidden-import=PIL._tkinter_finder',
        '--hidden-import=keyboard',
        '--hidden-import=mss',
        '--hidden-import=google.cloud.vision',
        '--hidden-import=easyocr',
        '--hidden-import=torch',
        '--hidden-import=torchvision',
        '--hidden-import=dotenv',
        '--hidden-import=requests',
        '--hidden-import=app.settings',
        '--hidden-import=app.settings_window',
        
        # データファイル（icon.pngをEXEと同じディレクトリに含める）
        '--add-data=icon.png;.',  # タスクトレイアイコン用
        
        # 除外するモジュール（不要なライブラリを除外してサイズを削減）
        '--exclude-module=matplotlib',
        '--exclude-module=scipy',
        '--exclude-module=pandas',
        
        # その他のオプション
        '--noupx',                    # UPX圧縮を無効化（エラー回避のため）
    ]
    
    # アイコンファイルがある場合
    icon_path = 'icon.ico'
    if os.path.exists(icon_path):
        args.append(f'--icon={icon_path}')
    
    # PyInstallerを実行
    PyInstaller.__main__.run(args)

if __name__ == '__main__':
    build()

