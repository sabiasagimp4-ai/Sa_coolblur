# 移植範囲

移植元コミット: `9714c89623fa44525b6cbbbe562007d9decf06cf`。

元の `cool_blur` のGPUギャザー処理（`CoolBlur_Cuda.cu`）を基準に、Direct2DのHLSLへ移植。YMM4ラッパーはSa_aohueの構成を参照。CPU版のFFTではなく、元GPU版と同じハイブリッドVogelギャザーを使う。

- 帯状、円形、深度、全体のピント計算と反転
- 分散の3半径、色のチャンネルごとの重み正規化、全色黒のRGBフォールバック
- 楕円の面積を保つ縦横比、ボケの縁の重み、ハイライトのべき乗変換
- sRGBの変換前後でアルファを除算・乗算
- 小半径は円盤の全点。大半径は元GPU版と同じ64 / 128 / 256 / 512点のハイブリッドVogel点群
- 半径16px以下は64点、40px以下は128点、それ以上は画質設定（128 / 256 / 512）
- 画像の端の引き延ばし／透明、半径0・ピント内の原画保持
- 元GPU版と同じAuto downsample（32px以上で2倍、96px以上で4倍）。前処理済み画像をcontent-onlyでbox平均し、低解像度でギャザー、線形補間で拡大してフル解像度の焦点量で原画と合成

## 一致範囲と差

- Direct2D GPU専用。AE CPU版のFFTとOpenCLの実行環境は持たない。
- 半径・分散・重み・色管理・アルファ処理は、AE GPU版の通常解像度パスとAuto downsampleパスの計算順にそろえている。
- D3D11とCUDAでは浮動小数点・テクスチャ補間の実装が異なるため、異なるGPU間で8bit画像の全ピクセルがバイト単位で一致することまでは保証できない。式とパラメータの挙動は一致させる。
- 深度は静止画ファイルのみ。別レイヤー、動画、OpenEXR深度の取り込みは未対応。
- 出力は元GPU版に合わせたpremultiplied alpha。RGBをalphaへ追加クランプしない。
- 出力領域は元の素材サイズ。素材の外へのボケ拡張は行わない。
- CPU読戻しなし。中間バッファは32bit float、前処理でガンマ変換とハイライト処理を済ませる。

## ビルド

Windows / .NET 10 / Windows SDK、YMM4 LiteのDLLを使用。

```powershell
dotnet build ymm/SaCoolBlurYmm.csproj -c Release -p:YMM4DirPath="C:\YMM4\" -p:FxcPath="C:\path\fxc.exe" -p:D2DIncludePath="C:\path\um"
```

GitHub Actionsでも同じ構成でC#・HLSLをコンパイルし、配布ZIPを作る。

## 検証

2026-09-21: [Windows CI](https://github.com/sabiasagimp4-ai/Sa_coolblur/actions/runs/35584059978) 成功。

- YMM4 Liteの実DLLを参照してC#とHLSLをビルド。警告・エラーなし。
- 本番HLSLをD3D11 WARPで描画し38ケース成功。全画質・ボケの縁・正負の分散、半透明の一定色、半径0、ピント内、完全透明、深度未指定／焦点一致、端の透明化、色の正規化、点光源の分散を確認。Auto downsampleの縮小・合成シェーダーも本番FXCでコンパイル済み。
- WARPテストはDirect2Dの座標ヘルパーをテスト用アダプターで置換して実行する。YMM4のUI、Direct2Dグラフ全体、プロジェクト保存／再読込、実GPU性能までは確認していない。
- YMM4実機でのプレビュー・書き出し確認は別途必要。
