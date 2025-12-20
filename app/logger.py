"""
ログ出力ユーティリティ
"""
import os
import logging
import sys
from pathlib import Path
from dotenv import load_dotenv

# 環境変数を読み込む
load_dotenv(override=True)

# ログファイルのパスを取得（プロジェクトルートまたは実行ファイルのディレクトリ）
def _get_log_path():
    """ログファイルのパスを取得"""
    if getattr(sys, 'frozen', False):
        # PyInstallerでビルドされたEXEの場合
        log_dir = Path(sys.executable).parent
    else:
        # スクリプトとして実行される場合
        log_dir = Path(__file__).parent.parent
    return log_dir / 'translator.log'

# ロガーを初期化
_logger = None

def _init_logger():
    """ロガーを初期化"""
    global _logger
    if _logger is not None:
        return _logger
    
    _logger = logging.getLogger('OverlayTranslator')
    _logger.setLevel(logging.DEBUG)
    
    # 既存のハンドラーをクリア
    _logger.handlers.clear()
    
    # ファイルハンドラーを設定
    log_path = _get_log_path()
    file_handler = logging.FileHandler(log_path, encoding='utf-8', mode='a')
    file_handler.setLevel(logging.DEBUG)
    
    # フォーマッターを設定
    formatter = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    file_handler.setFormatter(formatter)
    
    _logger.addHandler(file_handler)
    
    return _logger

def _is_debug_enabled():
    """DEBUGログが有効かどうかを確認"""
    debug_value = os.getenv('DEBUG_LOGGING', 'false').lower()
    return debug_value in ('true', '1', 'yes', 'on')

def get_logger():
    """ロガーインスタンスを取得"""
    if _logger is None:
        _init_logger()
    return _logger

def log(level, message, *args, **kwargs):
    """ログを出力
    
    Args:
        level: ログレベル (logging.DEBUG, logging.INFO, logging.WARNING, logging.ERROR)
        message: ログメッセージ
        *args, **kwargs: logging.log()に渡す追加引数
    """
    logger = get_logger()
    
    # DEBUGレベルの場合は設定を確認
    if level == logging.DEBUG and not _is_debug_enabled():
        return
    
    logger.log(level, message, *args, **kwargs)

def debug(message, *args, **kwargs):
    """DEBUGログを出力（設定が有効な場合のみ）"""
    log(logging.DEBUG, message, *args, **kwargs)

def info(message, *args, **kwargs):
    """INFOログを出力"""
    log(logging.INFO, message, *args, **kwargs)

def warning(message, *args, **kwargs):
    """WARNINGログを出力"""
    log(logging.WARNING, message, *args, **kwargs)

def error(message, *args, **kwargs):
    """ERRORログを出力"""
    log(logging.ERROR, message, *args, **kwargs)

def exception(message, *args, exc_info=True, **kwargs):
    """例外情報を含むERRORログを出力"""
    logger = get_logger()
    logger.error(message, *args, exc_info=exc_info, **kwargs)
