# ログファイルを確認するスクリプト

Write-Host "=== OverlayTranslator ログファイル確認 ===" -ForegroundColor Cyan
Write-Host ""

# ログファイルのパスを探す
$logPaths = @(
    "$env:LOCALAPPDATA\Packages\*\LocalState\translator.log",
    "$env:APPDATA\OverlayTranslator\translator.log",
    "$PSScriptRoot\bin\Debug\net10.0-windows10.0.19041.0\translator.log"
)

$logFile = $null
foreach ($path in $logPaths)
{
    $found = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found)
    {
        $logFile = $found.FullName
        break
    }
}

if ($logFile -and (Test-Path $logFile))
{
    Write-Host "ログファイルが見つかりました: $logFile" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== 最新のログ（最後の20行） ===" -ForegroundColor Yellow
    Write-Host ""
    Get-Content $logFile -Tail 20
    Write-Host ""
    Write-Host "=== ログファイルのサイズ ===" -ForegroundColor Yellow
    $fileInfo = Get-Item $logFile
    Write-Host "サイズ: $($fileInfo.Length) バイト"
    Write-Host "更新日時: $($fileInfo.LastWriteTime)"
}
else
{
    Write-Host "ログファイルが見つかりませんでした。" -ForegroundColor Red
    Write-Host ""
    Write-Host "確認したパス:" -ForegroundColor Yellow
    foreach ($path in $logPaths)
    {
        Write-Host "  - $path"
    }
    Write-Host ""
    Write-Host "アプリを起動してから再度実行してください。" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== 完了 ===" -ForegroundColor Cyan

