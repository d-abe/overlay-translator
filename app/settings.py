"""
設定管理機能
"""
import os
import sys
from pathlib import Path
from dotenv import load_dotenv, set_key, find_dotenv

class SettingsManager:
    def __init__(self):
        self.env_path = self._get_env_path()
        # 既存の設定を読み込み
        if os.path.exists(self.env_path):
            load_dotenv(self.env_path)
    
    def _get_env_path(self):
        """.envファイルのパスを取得"""
        # 実行ファイルと同じディレクトリを優先
        if getattr(sys, 'frozen', False):
            # exeファイルとして実行されている場合
            base_path = Path(sys.executable).parent
        else:
            # Pythonスクリプトとして実行されている場合
            base_path = Path(__file__).parent.parent
        
        env_path = base_path / '.env'
        return str(env_path)
    
    def get(self, key, default=None):
        """設定値を取得"""
        return os.getenv(key, default)
    
    def set(self, key, value):
        """設定値を設定（.envファイルに保存）"""
        if value is None or (isinstance(value, str) and value.strip() == ''):
            # 空の値の場合は削除
            self._remove_key(key)
        else:
            set_key(self.env_path, key, str(value))
            # 環境変数にも反映
            os.environ[key] = str(value)
    
    def _remove_key(self, key):
        """設定キーを削除"""
        if not os.path.exists(self.env_path):
            return
        
        # .envファイルから該当行を削除
        lines = []
        with open(self.env_path, 'r', encoding='utf-8') as f:
            for line in f:
                if not line.strip().startswith(f'{key}='):
                    lines.append(line)
        
        with open(self.env_path, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        
        # 環境変数からも削除
        if key in os.environ:
            del os.environ[key]
    
    def get_all(self):
        """すべての設定を取得"""
        return {
            'GROQ_API_KEY': self.get('GROQ_API_KEY', ''),
            'GROQ_MODEL': self.get('GROQ_MODEL', 'llama-3.1-8b-instant'),
            'GOOGLE_API_KEY': self.get('GOOGLE_API_KEY', ''),
            'GOOGLE_APPLICATION_CREDENTIALS': self.get('GOOGLE_APPLICATION_CREDENTIALS', ''),
            'OCR_METHOD': self.get('OCR_METHOD', 'google'),
            'HOTKEY': self.get('HOTKEY', 'ctrl+shift+t'),
            'OVERLAY_FONT_FAMILY': self.get('OVERLAY_FONT_FAMILY', 'Meiryo'),
            'OVERLAY_FONT_SIZE': self.get('OVERLAY_FONT_SIZE', '12'),
            'OVERLAY_FONT_STYLE': self.get('OVERLAY_FONT_STYLE', 'normal'),
            'DEBUG_LOGGING': self.get('DEBUG_LOGGING', 'false'),
        }
    
    def save_all(self, settings_dict):
        """すべての設定を保存"""
        for key, value in settings_dict.items():
            self.set(key, value)
        
        # 環境変数を再読み込み
        load_dotenv(self.env_path, override=True)

