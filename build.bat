@echo off
REM exeファイルをビルドするバッチファイル

echo Overlay Translator - exeファイルのビルドを開始します...
echo.

REM PyInstallerがインストールされているか確認
python -c "import PyInstaller" 2>nul
if errorlevel 1 (
    echo PyInstallerがインストールされていません。
    echo 開発用依存関係をインストールします...
    pip install -r requirements-dev.txt
    if errorlevel 1 (
        echo 依存関係のインストールに失敗しました。
        pause
        exit /b 1
    )
)

echo exeファイルをビルドします...
python build_exe.py

if errorlevel 1 (
    echo ビルドに失敗しました。
    pause
    exit /b 1
)

echo.
echo ビルドが完了しました！
echo exeファイルは dist\OverlayTranslator.exe に生成されました。
echo.
pause

