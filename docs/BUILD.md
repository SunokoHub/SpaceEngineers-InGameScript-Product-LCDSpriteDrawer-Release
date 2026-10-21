# ビルド

Windows、.NET SDK 9.0.318、.NET Framework 4.8の開発用参照、およびSpace Engineers 1の実ゲームDLLを使います。
C# 6を対象とし、Mod専用APIやスタブDLLは使用しません。部品ソースは同梱され、別の非公開リポジトリへのアクセスは不要です。

1. 公開リポジトリを取得し、SDK 9.0.318を用意します。
2. `src/IngameScript/mdk.local.ini` をローカルで作成します。`binarypath` は自分のゲームのBin64へ変更してください。このファイルはコミットしません。

```ini
[mdk]
binarypath=<your Space Engineers Bin64 directory>
output=../../artifacts
interactive=DoNothing
```

3. リポジトリルートで実行します。

```powershell
dotnet restore src/IngameScript/IngameScript.csproj
dotnet build src/IngameScript/IngameScript.csproj -c Release --no-restore
```

出力は `artifacts/IngameScript/script.cs` です。MDKはPbAnalyzers 2.1.17、PbPackager 2.2.10、References 2.2.9を使用します。
短縮設定は `lite`、`nomembertrimming`、`trimUnusedTypes=false` です。PLAYER SETUPとMIT表示は保持領域にあります。

生成本文を確認します（Windows PowerShell 5.1）。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-generated-script.ps1 -GameBin '<your Space Engineers Bin64 directory>' -ScriptPath artifacts/IngameScript/script.cs
Get-FileHash artifacts/IngameScript/script.cs -Algorithm SHA256
```

検査は実ゲーム参照のC# 6コンパイル・ASCII・先頭ガイド・100,000文字以内を確認します。ゲーム内のwhitelist・実行は別の接点です。
公式本文のハッシュは `release-manifest.json` にあります。ゲームDLLやSDK、改行条件の違いで生成バイト列が変わり得るため、不一致を直ちに改ざんと扱わないでください。
改造版は自分の変更を記録し、元版との区別を付けることを推奨します。
