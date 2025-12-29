@echo off
REM WinUI 3アプリケーションを発行するバッチファイル

echo ========================================
echo Overlay Translator - 発行スクリプト
echo ========================================
echo.

REM プロジェクトディレクトリに移動
cd /d "%~dp0"

echo Release構成で自己完結型として発行します...
echo.

REM 発行を実行
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:PublishTrimmed=true

if errorlevel 1 (
    echo.
    echo 発行に失敗しました。
    pause
    exit /b 1
)

echo.
echo ========================================
echo 発行が完了しました！
echo ========================================
echo.
echo 発行先: bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\
echo.
echo 配布する場合は、publishフォルダ内のすべてのファイルを配布してください。
echo.
pause


