# WinUI 3版 配布手順

## 概要

WinUI 3アプリケーションをexeファイルとして配布する方法を説明します。

## 配布方法

### 方法1: 自己完結型（Self-contained）デプロイ（推奨）

すべての依存関係を含むため、ユーザーのPCに.NETランタイムがインストールされていなくても動作します。

#### コマンドラインから発行

```powershell
# プロジェクトディレクトリに移動
cd winui3\OverlayTranslator

# Release構成で自己完結型として発行
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:PublishTrimmed=true
```

#### 発行先

発行されたファイルは以下のディレクトリに生成されます：
```
bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\
```

#### 配布に必要なファイル

発行ディレクトリ内の**すべてのファイル**を配布する必要があります：

```
publish/
├── OverlayTranslator.exe          # メイン実行ファイル
├── OverlayTranslator.dll          # アプリケーションDLL
├── OverlayTranslator.pdb          # デバッグ情報（オプション）
├── Assets/                        # アセットファイル
│   ├── icon.ico
│   └── ...
├── *.dll                          # 依存DLL（多数）
└── *.json                         # 設定ファイル
```

**重要**: すべてのファイルを同じディレクトリに配置して配布してください。

### 方法2: 単一ファイル（Single-file）デプロイ

すべてのファイルを1つのexeファイルにまとめます（.NET 6以降で利用可能）。

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=true
```

**注意**: WinUI 3アプリでは、一部のネイティブDLLが単一ファイルに含まれない場合があります。完全な動作を保証するには、方法1（複数ファイル）を推奨します。

### 方法3: MSIXパッケージ化

Windows StoreやMicrosoft Store経由で配布する場合に使用します。

1. Visual Studioでプロジェクトを開く
2. ソリューションエクスプローラーでプロジェクトを右クリック
3. 「パッケージ化と発行」→「新しいアプリケーション パッケージの作成」を選択
4. 配布方法を選択（Microsoft Store、サイドローディングなど）

## 配布パッケージの作成

### 手順

1. **発行を実行**
   ```powershell
   cd winui3\OverlayTranslator
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:PublishTrimmed=true
   ```

2. **配布用フォルダを作成**
   ```
   OverlayTranslator-Distribution/
   └── OverlayTranslator/
       └── (publishフォルダの全内容をコピー)
   ```

3. **README.txtを作成**（オプション）
   ```
   Overlay Translator - 使い方

   1. このフォルダ内のすべてのファイルを同じ場所に配置してください
   2. OverlayTranslator.exeを実行してください
   3. 初回起動時、appsettings.jsonが自動的に作成されます
   4. タスクトレイアイコンを右クリックして「設定」からAPIキーを設定してください

   システム要件:
   - Windows 10 バージョン 1809以降 / Windows 11
   - .NETランタイムは不要です（自己完結型パッケージ）
   ```

4. **ZIPファイルに圧縮**（配布用）
   ```
   OverlayTranslator-v1.0.0-win-x64.zip
   ```

## 配布時の注意事項

### 必須ファイル

- `OverlayTranslator.exe` - メイン実行ファイル
- `OverlayTranslator.dll` - アプリケーションDLL
- `Assets/icon.ico` - アイコンファイル
- すべての依存DLL（`*.dll`）
- 設定ファイル（`*.json`、`*.pri`など）

### オプションファイル

- `OverlayTranslator.pdb` - デバッグ情報（配布時は削除可能）
- `README.txt` - 使い方説明

### 不要なファイル

- `obj/` フォルダ
- `bin/` フォルダ（publish以外）
- `.csproj` ファイル
- `.sln` ファイル
- ソースコード（`.cs`、`.xaml`）

## ユーザーへの配布手順

1. **ZIPファイルをダウンロード**
2. **ZIPファイルを解凍**
3. **フォルダ内のすべてのファイルを同じ場所に配置**（重要）
4. **OverlayTranslator.exeを実行**
5. **初回起動時、appsettings.jsonが自動的に作成される**
6. **タスクトレイアイコンを右クリック→「設定」からAPIキーを設定**

## トラブルシューティング

### 問題: アプリが起動しない

**原因**: 必要なDLLが不足している可能性があります。

**解決方法**:
- すべてのファイルが同じディレクトリにあることを確認
- Windows 10 バージョン 1809以降 / Windows 11であることを確認
- Visual C++ 再頒布可能パッケージが必要な場合があります

### 問題: アイコンが表示されない

**原因**: `Assets/icon.ico`ファイルが不足している可能性があります。

**解決方法**:
- `Assets/icon.ico`ファイルが配布パッケージに含まれていることを確認

### 問題: 設定が保存されない

**原因**: 書き込み権限がない可能性があります。

**解決方法**:
- 管理者権限で実行する必要はありませんが、書き込み可能な場所に配置してください
- Program Filesなどの保護されたディレクトリには配置しないでください

## サイズの最適化

### トリミング（Trimming）

Release構成で発行する場合、`PublishTrimmed=true`により未使用のコードが削除され、サイズが削減されます。

### ReadyToRun（R2R）

`PublishReadyToRun=true`により、起動時間が短縮されます。

## バージョン情報の設定

`app.manifest`ファイルでバージョン情報を設定できます。

## 参考リンク

- [.NET アプリケーションの発行](https://learn.microsoft.com/ja-jp/dotnet/core/deploying/)
- [WinUI 3 アプリのパッケージ化](https://learn.microsoft.com/ja-jp/windows/apps/package-and-deploy/)


