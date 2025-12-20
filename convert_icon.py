"""
icon.pngをicon.icoに変換するスクリプト
EXEファイルのアイコンとして使用するために必要です
"""
from PIL import Image
import os

def convert_png_to_ico():
    """icon.pngをicon.icoに変換"""
    png_path = 'icon.png'
    ico_path = 'icon.ico'
    
    if not os.path.exists(png_path):
        print(f"エラー: {png_path}が見つかりません")
        return False
    
    try:
        # PNG画像を読み込む
        img = Image.open(png_path)
        
        # 複数のサイズを含むICOファイルを作成（Windowsで推奨）
        # 16x16, 32x32, 48x48, 256x256のサイズを含める
        sizes = [(16, 16), (32, 32), (48, 48), (256, 256)]
        img.save(ico_path, format='ICO', sizes=sizes)
        
        print(f"成功: {png_path}を{ico_path}に変換しました")
        return True
    except Exception as e:
        print(f"エラー: 変換に失敗しました: {e}")
        return False

if __name__ == "__main__":
    convert_png_to_ico()

