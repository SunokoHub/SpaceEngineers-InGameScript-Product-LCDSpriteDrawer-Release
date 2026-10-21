# ソースの読み方

短縮前のソースは `src/IngameScript`、生成本文は `dist` にあります。編集は短縮前ソースで行ってください。
まずProgram、SetupConfig、StaticLayout、Binding、Motion、描画経路の順に読むと全体を追えます。

| ファイル | 主な責務 |
| --- | --- |
| Program.cs | コマンド、分割実行、ブロック探索、参照値の一巡キャッシュ、状態表示と停止 |
| SurfaceInfo.cs | 選択タグ、表示面情報、infoコマンドの出力 |
| SetupConfig.cs | CustomDataの解析・不足設定補完、所有者と面ごとの設定検証 |
| StaticLayout.cs | Layoutの字句解析と静止要素の検証。Motion/Bindingも属性解析を共有 |
| Motion.cs | 時間からの属性計算、固定点・寸法・向き・公転・移動の変換 |
| Binding.cs | Sourceを数値・色・文字・可視性へ変換し、Motionと合成 |
| BlockValues.cs | 30種類のブロック値の型判定とPB APIからの取得 |
| DrawingRuntime.cs | 描画対象の保持、Layoutから型付き描画要素への変換 |
| SurfacePass.cs | 一面32要素の上限と、面間での実行予算判断・続行位置 |
| RunState.cs | 私有Storageの検証、Running/Stopped資格と初回色の適用済み状態 |

`setup` は対象ごとに検証・保存し、最初の失敗で止まります。既に保存した対象は戻しません。
`reload` は設定と参照を段階的に検証してから描画を開始します。
描画の一巡では時刻と参照値を固定し、面間で処理を延期しても同じ値を使います。
各巡は基準Layoutから評価し、前フレームの変形へ累積しません。

`vendor/modules` のcustomDataは原文保持と不足キー補完、blockSelectionはタグ選択、spriteDrawingのCoreは型付き要素の検証・座標変換・フレーム送信を担います。
ファイルと固定版は [依存部品](DEPENDENCIES.md) を参照してください。文字列パーサー版のSpriteDrawing.csは使いません。

変更時は次の契約を維持してください。

- 他用途のCustomData、未知情報、コメントと末尾を保持し、不正文書を自動修復しない。
- 保存失敗・競合の後に後続保存や描画再開を行わない。
- stopは最終表示を残す。元の表示設定を捕捉・復元しない。
- Binding参照喪失時は依存面だけを除外し、交換品の検出は明示reloadで行う。
- 保存されるのは再開資格であり、アニメーション位相ではない。

`mdk.ini` のlite短縮により識別子が変わります。公開マニフェストと生成本文のSHA-256で対象を照合し、デバッグには短縮前ソースを使ってください。
