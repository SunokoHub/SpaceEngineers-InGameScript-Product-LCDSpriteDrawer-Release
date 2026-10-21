# 設定例

setupで作られた対象LCDのCustomDataを編集します。Owner、Tags、SurfaceInfoや他用途の内容は保持してください。
`[Settings.LCDSpriteDrawer]` のSurfacesが対象面の番号で、面0なら `[Settings.LCDSpriteDrawer.Surface.0]` の値を編集します。
Ownerはsetupを実行したPBのIDです。別PBへ移す場合は旧PBをstopしてから、利用者が所有者を明示的に変更します。

## 静止表示

面0の既存Layoutを次のように変更します。MyIniの複数行値では、継続行を `|` で始めます。
他の面・セクションをこの例で置き換えないでください。

```ini
[Settings.LCDSpriteDrawer.Surface.0]
Layout=
|Id=box;Type=TEXTURE;Data="SquareSimple";Position=0,0;Size=120,60;Color=0,160,255,255
|Id=label;Type=TEXT;Data="HELLO";Position=0,80;TextScale=1;Color=255,255,255,255
Motion=
Binding=
```

reloadすると表示します。位置はTextureSizeの中心からのオフセットで、xは右、yは下が正です。
TEXTUREはSizeが必要です。TEXTにはSizeやAngleDegreesを指定できません。
レコードの属性はセミコロン区切り、名前と列挙値は大小文字を区別します。IDは英字始まり、英数字とアンダースコアの32文字以内です。
文字列は二重引用符で囲み、`\"`、`\\`、`\n`、`\r`、`\t` を使えます。内部コメントは使えません。
ColorはRGBAの整数0〜255。省略時は面のScriptForegroundColorです。TEXTのFontIdは省略時Debug、AlignmentはLEFT/CENTER/RIGHTです。

## 時間アニメーション

上記のMotionを次に変えるとboxが左右に往復します。

```ini
Motion=
|Element=box;Property=MoveX;Rate=30;Initial=0;Min=-100;Max=100;Mode=PingPong
```

PropertyはMoveX/MoveY/Width/Height/Angle/TextScale/Orbitです。Rateは秒当たりの変化量、角度系は度を使います。
ModeはClamp/Loop/PingPong。Loop/PingPongにはMinとMaxが必要です。TextScaleはTEXT専用、Width/Height/AngleはTEXTURE専用です。
Orbitは位置だけの公転で、Center=x,yが必要です。向きの回転には別のAngleを指定します。
Anchorは寸法・向きの変化で固定する点で、TopLeft/TopCenter/TopRight/CenterLeft/Center/CenterRight/BottomLeft/BottomCenter/BottomRightを指定します。省略時はCenterです。同じ要素のWidth/Heightには同じAnchorを使います。

## ブロック値との連動

同じ構成にある一意な名前 `Reference Cockpit` のコックピットを用意し、Bindingを次へ変更します。

```ini
Binding=
|Element=label;Property=Data;Source=Speed;BlockName="Reference Cockpit";Decimals=1;Prefix="Speed: ";Suffix=" m/s"
|Element=box;Property=Color;Source=Speed;BlockName="Reference Cockpit";InputMin=0;InputMax=100;MinColor=0,255,0,255;MaxColor=255,0,0,255
```

参照ブロックにタグやOwnerは不要です。数値属性への接続はOutputMin/OutputMaxを使い、数値SourceにはInputMin/InputMaxが必要です。
Mode=Clamp/Loop/PingPongで入力の範囲処理を選べます。真偽SourceにはInputMin/InputMaxを付けず、0/1として扱います。
Data/VisibleはThresholdsとCase0〜CaseNで切替できます。例えば `Thresholds=0.5;Case0=false;Case1=true` です。
境界一致は上側の区間へ進みます。DataのCaseは引用文字列、Visibleは非引用true/falseです。
同一属性のMotionとBindingを併用するときはCombine=Replace/Add/Multiplyを明示してください。

| Source | 対象・単位 |
| --- | --- |
| Speed | 船舶コントローラー、m/s |
| Enabled | 機能ブロック、真偽 |
| IsWorking / IsFunctional | ターミナルブロック、真偽 |
| PowerOutput / PowerMaxOutput | 発電ブロック、MW |
| BatteryStoredPower / BatteryMaxStoredPower | バッテリー、MWh |
| BatteryChargeRatio / BatteryInput / BatteryMaxInput / BatteryIsCharging | バッテリー、比率 / MW / MW / 真偽 |
| GasFillRatio / GasCapacity | ガスタンク、比率 / L |
| DoorOpenRatio | ドア、比率 |
| PistonPosition / PistonVelocity | ピストン、m / 設定速度m/s |
| RotorAngle / RotorVelocity / RotorLocked | ローター、rad / 設定速度rpm / 真偽 |
| ShipBaseMass / ShipTotalMass / ShipPhysicalMass | 船舶コントローラー、kg |
| NaturalGravity / ArtificialGravity / TotalGravity | 船舶コントローラー、加速度の大きさm/s^2 |
| AngularSpeed | 船舶コントローラー、角速度の大きさrad/s |
| IsUnderControl / HandBrake / Dampeners | 船舶コントローラー、真偽 |

SourceのRotorAngleはrad、出力のAngle/Orbitは度です。必要ならInputMin/InputMaxとOutputMin/OutputMaxで変換してください。
テキストの数値Dataは元の値を表示し、自動単位変換はしません。
