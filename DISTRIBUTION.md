# 配布パッケージの内容

EXE化したアプリケーションを配布する際に必要なファイルは以下の通りです。

## 必須ファイル

### 1. `OverlayTranslator.exe`
- メインの実行ファイル
- `dist/`ディレクトリに生成されます
- すべての依存関係が含まれています（Pythonのインストールは不要）

### 2. `.env.example`
- 設定ファイルのテンプレート
- ユーザーが`.env`ファイルを作成する際の参考として使用
- 実際のAPIキーは含まれていません

### 3. `icon.png`（推奨）
- タスクトレイアイコン用の画像ファイル
- EXEファイルと同じディレクトリに配置してください
- 注意: EXEファイルに`icon.png`が含まれている場合でも、実行時に同じディレクトリに`icon.png`が必要です

## 配布時の手順

1. **配布用フォルダを作成**
   ```
   OverlayTranslator/
   ├── OverlayTranslator.exe
   ├── .env.example
   └── icon.png  (推奨)
   ```

2. **ユーザーへの説明**
   - `.env.example`を`.env`にコピー
   - `.env`ファイルを開いて、APIキーを設定
   - `OverlayTranslator.exe`を実行

## オプション（推奨）

### `README.txt` または `使い方.txt`
簡易的な使い方説明を記載すると親切です。

例：
```
Overlay Translator - 使い方

1. .env.exampleを.envにコピーしてください
2. .envファイルを開いて、以下のAPIキーを設定してください：
   - GROQ_API_KEY: Groq APIキー（翻訳用）
   - GOOGLE_API_KEY: Google Cloud Vision APIキー（OCR用、オプション）
   - OCR_METHOD: google, groq, または easyocr を選択
3. OverlayTranslator.exeを実行してください
4. タスクトレイにアイコンが表示されます
5. 設定画面からAPIキーやホットキーを設定できます

詳細は設定画面の各項目の説明を参照してください。
```

## 注意事項

- **不要なファイル**: `requirements.txt`、Pythonスクリプト、`icon.ico`などは不要です
- **icon.png**: EXEファイルに含まれていますが、実行時に同じディレクトリに`icon.png`が必要です（タスクトレイアイコン表示のため）
- **ログファイル**: 実行時に`translator.log`が自動生成されます（ユーザーが削除可能）
- **EasyOCRモデル**: `OCR_METHOD=easyocr`を選択した場合、初回実行時に約500MBのモデルファイルがダウンロードされます

## ファイルサイズ

- `OverlayTranslator.exe`: 約200-500MB（EasyOCRを含む場合）
- `.env.example`: 数KB

## 配布形式

- ZIPファイルに圧縮して配布することを推奨します
- 例: `OverlayTranslator-v1.0.zip`

