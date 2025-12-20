# Overlay Translator

Windows上で動作するデスクトップアプリケーション。画面上の選択範囲をキャプチャし、AIで翻訳してオーバーレイ表示します。

## 機能

- タスクトレイに常駐
- ホットキーで矩形選択
- 選択範囲の画像キャプチャ
- OCRによるテキスト抽出（Google Cloud Vision API、Groq Vision API、またはEasyOCR）
- AI翻訳（日本語）
- オーバーレイ表示（背景色に合わせた表示）

## セットアップ

1. Python 3.8以上をインストール

2. 依存関係をインストール:
   
   **開発環境の場合（推奨）:**
   ```
   pip install -r requirements-dev.txt
   ```
   - これには実行に必要な依存関係と、exeビルド用のPyInstallerが含まれます
   
   **実行のみの場合:**
   ```
   pip install -r requirements.txt
   ```
   - アプリケーションの実行に必要な最小限の依存関係のみ
   
   **注意**: EasyOCRは初回実行時にモデルファイルをダウンロードします（約500MB）。時間がかかる場合があります。

3. APIキーを設定:
   
   **方法1: 設定画面から設定（推奨）**
   - アプリケーションを起動後、タスクトレイアイコンを右クリック
   - 「設定」を選択して設定画面を開く
   - 各APIキーを入力して「保存」をクリック
   
   **方法2: .envファイルで直接設定**
   - `.env.example`ファイルをコピーして`.env`ファイルを作成
   - `.env`ファイルを開き、実際のAPIキーを設定:
     ```
     HOTKEY=ctrl+shift+t
     GROQ_API_KEY=your_groq_api_key_here
     GROQ_MODEL=llama-3.1-8b-instant
     GROQ_VISION_MODEL=meta-llama/llama-4-scout-17b-16e-instruct
     GOOGLE_API_KEY=your_google_api_key_here
     OCR_METHOD=google
     ```
   
   - Groq APIキー: https://console.groq.com/ で取得
   - Google APIキー: Google Cloud Consoleで取得
   - 利用可能なGroqモデル（翻訳用）: 最新のモデルリストは [Groq公式ドキュメント](https://console.groq.com/docs/models) を参照
     - 推奨: `llama-3.1-8b-instant`（高速・低コスト）
     - 高品質: `llama-3.3-70b-versatile`, `openai/gpt-oss-120b`
   - Groq Visionモデル（OCR用）: [Groq Vision APIドキュメント](https://console.groq.com/docs/vision) を参照
     - 推奨: `meta-llama/llama-4-scout-17b-16e-instruct`（高速）
     - 高品質: `meta-llama/llama-4-maverick-17b-128e-instruct`
   - OCR方法の選択:
     - `google`: Google Cloud Vision API（高精度・請求が必要）
     - `groq`: Groq Vision API（高速・Groq APIキー使用）
     - `easyocr`: EasyOCR（オフライン・初回のみモデルダウンロード）
   - ホットキー設定:
     - 矩形選択を開始するホットキーを設定（例: `ctrl+shift+t`, `alt+f1`）
     - 複数のキーは「+」で区切ります
     - 設定画面からも変更可能で、変更は即座に反映されます

## 使用方法

### Pythonで直接実行する場合

1. `python main.py`でアプリケーションを起動
2. デフォルトのホットキーは`Ctrl+Shift+T`
3. ホットキーを押すと矩形選択モードが有効になります
4. マウスで選択範囲をドラッグ
5. 選択範囲が自動的にキャプチャされ、翻訳結果がオーバーレイ表示されます

### exeファイルとしてビルドする場合

1. 開発用依存関係をインストール（まだインストールしていない場合）:
   ```
   pip install -r requirements-dev.txt
   ```
   **注意**: 既に `requirements-dev.txt` をインストールしている場合は、この手順は不要です。

2. exeファイルをビルド:
   
   **Windowsの場合（簡単）:**
   ```
   build.bat
   ```
   
   **手動でビルドする場合:**
   ```
   python build_exe.py
   ```
   または
   ```
   pyinstaller OverlayTranslator.spec
   ```

3. ビルドされたexeファイルは `dist/OverlayTranslator.exe` に生成されます

4. exeファイルを実行する前に、同じディレクトリに`.env`ファイルを配置してください

**注意事項:**
- **exeファイルにはすべての依存関係が含まれています** - `requirements.txt`やPythonのインストールは不要です
- exeファイルは初回起動時に少し時間がかかる場合があります（自己展開のため）
- **Google Vision APIを使用する場合**: EasyOCRのモデルファイルはダウンロードされません（不要です）
- **Groq Vision APIを使用する場合**: EasyOCRのモデルファイルはダウンロードされません（不要です）
- **EasyOCRを使用する場合**: 初回実行時にモデルファイルをダウンロードします（約500MB）
- `.env`ファイルはexeファイルと同じディレクトリに配置してください
- **Tesseract OCRのインストールは不要です**（Google Cloud Vision API、Groq Vision API、またはEasyOCRを使用）

**補足**: exeファイルにはEasyOCRのライブラリ自体が含まれますが、`OCR_METHOD=google`を設定している場合、EasyOCRは使用されず、モデルファイルもダウンロードされません。exeファイルサイズを削減したい場合は、PyInstallerの設定でEasyOCRを除外することも可能です（ただし、フォールバック機能は使用できなくなります）。

