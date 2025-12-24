# Overlay Translator - WinUI 3版

## プロジェクト構造

```
OverlayTranslator/
├── Models/              # データモデル
│   ├── Settings.cs
│   └── TranslationResult.cs
├── Services/            # ビジネスロジック
│   ├── OCRService.cs           # OCR機能（Groq Vision API）
│   ├── TranslationService.cs   # 翻訳機能（Groq API）
│   ├── TrayIconService.cs      # タスクトレイ機能
│   ├── HotkeyService.cs        # ホットキー機能
│   ├── ScreenCaptureService.cs # 画面キャプチャ機能
│   └── OverlayService.cs       # オーバーレイ表示機能
├── Utils/              # ユーティリティ
│   ├── ConfigManager.cs        # 設定管理
│   └── Logger.cs              # ログ出力
└── Views/              # UI
    └── MainPage.xaml
```

## 実装状況

### ✅ 完了
- プロジェクト構造の作成
- 基本クラスのスケルトン作成
- 設定管理（ConfigManager）
- ログ出力（Logger）
- OCRサービス（Groq Vision API）の基本実装
- 翻訳サービス（Groq API）の基本実装

### 🚧 実装中
- タスクトレイ機能（Win32 APIを使用）
- ホットキー機能（Win32 APIを使用）
- 画面キャプチャ機能（Windows.Graphics.Capture API）
- オーバーレイ表示機能
- 矩形選択機能
- 設定ウィンドウ

## 次のステップ

1. タスクトレイ機能の実装
2. ホットキー機能の実装
3. 矩形選択機能の実装
4. 画面キャプチャ機能の実装
5. オーバーレイ表示機能の実装
6. 設定ウィンドウの実装
7. メインアプリケーションロジックの統合

## 参考資料

- [WinUI 3 ドキュメント](https://learn.microsoft.com/ja-jp/windows/apps/winui/winui3/)
- [Windows AI テキスト認識 (OCR) API](https://learn.microsoft.com/ja-jp/windows/ai/apis/text-recognition)
- [Windows.Graphics.Capture API](https://learn.microsoft.com/ja-jp/windows/win32/api/windows.graphics.capture/)

