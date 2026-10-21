# 変更履歴

## 2.0.1 / Pre-release

- ソース・利用者資料・生成済みPB本文のMIT公開用パッケージを整備。
- 短縮後本文にも著作権表示とMIT本文を保持。動作・設定・保存形式は2.0.0-pre.2と同じ。
- reload後に各面を一度Advertisementsへ切り替える暫定対処を含む。描画欠落への復旧効果とPB初回色設定の実機確認は未完了。

## 旧1.0.0からの移行

差替え前に旧本文で `stop` を実行し、CustomDataを控えてください。
OrbitAngle/RotateWithOrbitは廃止し、公転位置のOrbitと向きのAngleを分けました。
BindingのProperty=TextはProperty=Dataへ、FalseColor/TrueColorはMinColor/MaxColorへ変更してください。
自動変換は行いません。ConfigFormatVersion=1、SurfaceInfoFormatVersion=1、StateVersion=1です。
旧本文へ戻す前もstopし、旧版で読める設定へ戻してください。
