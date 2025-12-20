# exeファイルのビルド手順

## 前提条件

1. Python 3.8以上がインストールされていること
2. すべての依存関係がインストールされていること
3. **Tesseract OCRのインストールは不要です**（Google Cloud Vision APIまたはEasyOCRを使用）

## ビルド方法

### 方法1: build_exe.pyを使用（推奨）

```bash
# 開発用依存関係をインストール
pip install -r requirements-dev.txt

# exeファイルをビルド
python build_exe.py
```

### 方法2: specファイルを直接使用

```bash
# 開発用依存関係をインストール
pip install -r requirements-dev.txt

# specファイルからビルド
pyinstaller OverlayTranslator.spec
```

## ビルド結果

- `dist/OverlayTranslator.exe` - 実行可能なexeファイル（単一ファイル）
- `build/` - ビルド時の一時ファイル（削除可能）

## 配布時の注意事項

**重要**: exeファイルは`--onefile`オプションでビルドされているため、すべての依存関係がexeファイルにバンドルされています。

### 不要なもの
- ❌ `requirements.txt` - すべてのPythonライブラリがexeに含まれています
- ❌ Pythonのインストール - exeファイルはスタンドアロンで動作します
- ❌ その他の依存関係 - すべてexeファイルに含まれています

### 必要なもの
1. **.envファイル**: exeファイルと同じディレクトリに`.env`ファイルを配置する必要があります。
   ```
   GROQ_API_KEY=your_groq_api_key_here
   GROQ_MODEL=llama-3.1-8b-instant
   GOOGLE_API_KEY=your_google_api_key_here
   OCR_METHOD=google
   ```
   - `OCR_METHOD`: `google`（Google Cloud Vision API）または`easyocr`（EasyOCRライブラリ）を指定
   - 指定しない場合: 自動的にGoogle Visionを試し、失敗したらEasyOCRにフォールバック
   - サービスアカウントJSONファイルを使用する場合: `GOOGLE_APPLICATION_CREDENTIALS=path/to/key.json`を設定

2. **OCR方法**: 
   - **Google Cloud Vision APIを使用する場合** (`OCR_METHOD=google`):
     - `GOOGLE_API_KEY`または`GOOGLE_APPLICATION_CREDENTIALS`が必要です
     - EasyOCRのモデルファイルは**ダウンロードされません**（不要です）
     - インターネット接続が必要です
   - **EasyOCRを使用する場合** (`OCR_METHOD=easyocr`):
     - 初回実行時にモデルファイル（約500MB）をダウンロードします
     - オフラインで動作可能です
   - `.env`ファイルで`OCR_METHOD=google`または`OCR_METHOD=easyocr`を指定できます

3. **サービスアカウントJSONファイル**（使用する場合）:
   - `.env`ファイルで指定したパスにJSONファイルを配置してください
   - または、exeファイルと同じディレクトリに配置して相対パスで指定することもできます

4. **初回起動**: exeファイルは初回起動時に少し時間がかかる場合があります（自己展開のため）

## トラブルシューティング

### ビルドエラーが発生する場合

1. PyInstallerを最新版に更新:
   ```
   pip install --upgrade pyinstaller
   ```

2. クリーンビルドを試す:
   ```
   pyinstaller --clean OverlayTranslator.spec
   ```

### exeファイルが起動しない場合

1. コマンドプロンプトから実行してエラーメッセージを確認:
   ```
   dist\OverlayTranslator.exe
   ```

2. 必要なDLLが不足している可能性があります。エラーメッセージを確認してください。

### モジュールが見つからないエラー

`OverlayTranslator.spec`の`hiddenimports`に必要なモジュールを追加してください。

## アイコンの設定

`icon.ico`ファイルをプロジェクトルートに配置すると、exeファイルにアイコンが設定されます。

## ファイルサイズの削減

不要なモジュールを除外する場合は、`OverlayTranslator.spec`の`excludes`リストに追加してください。

