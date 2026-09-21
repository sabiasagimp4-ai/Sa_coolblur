# 移植範囲

移植元コミット: `9714c89623fa44525b6cbbbe562007d9decf06cf`。

元の `cool_blur` の `CoolBlur_Core.h`、`CoolBlur.cpp` の計算式をDirect2DのHLSLに移植。YMM4ラッパーはSa_aohueの構成を参照。

- 帯状、円形、深度、全体のピント計算と反転
- 分散の3半径、色のチャンネルごとの重み正規化、全色黒のRGBフォールバック
- 楕円の面積を保つ縦横比、ボケの縁の重み、ハイライトのべき乗変換
- sRGBの変換前後でアルファを除算・乗算
- 半径が小さい場合は円盤の全点、その他は重心補正したVogel点群
- 128 / 256 / 512点の固定画質。点群は時間に依存しない
- 画像の端の引き延ばし／透明、半径0・ピント内の原画保持

## AE版との差

- Direct2D GPU専用。AE CPU版のFFT、CUDA、OpenCL、低解像度化の設定は持たない。
- AE CPUのFFT出力とは完全一致しない。大きい半径はサンプル近似。
- 深度は静止画ファイルのみ。別レイヤー、動画、OpenEXR深度の取り込みは未対応。
- 出力はYMM4の合成に合わせてpremultiplied alphaを守る。RGBを0〜alphaに収めるため、AEのHDR出力とは一致しない。
- 出力領域は元の素材サイズ。素材の外へのボケ拡張は行わない。
- CPU読戻しなし。中間バッファは32bit float、前処理でガンマ変換とハイライト処理を済ませる。

## ビルド

Windows / .NET 10 / Windows SDK、YMM4 LiteのDLLを使用。

```powershell
dotnet build ymm/SaCoolBlurYmm.csproj -c Release -p:YMM4DirPath="C:\YMM4\" -p:FxcPath="C:\path\fxc.exe" -p:D2DIncludePath="C:\path\um"
```

GitHub Actionsでも同じ構成でC#・HLSLをコンパイルし、配布ZIPを作る。

## 検証

2026-09-21: [Windows CI](https://github.com/sabiasagimp4-ai/Sa_coolblur/actions/runs/35551222414) 成功。

- YMM4 Liteの実DLLを参照してC#とHLSLをビルド。警告・エラーなし。
- 本番HLSLをD3D11 WARPで描画し38ケース成功。全画質・ボケの縁・正負の分散、半透明の一定色、半径0、ピント内、完全透明、深度未指定／焦点一致、端の透明化、色の正規化、点光源の分散を確認。
- WARPテストはDirect2Dの座標ヘルパーをテスト用アダプターで置換して実行する。YMM4のUI、Direct2Dグラフ全体、プロジェクト保存／再読込、実GPU性能までは確認していない。
- YMM4実機でのプレビュー・書き出し確認は別途必要。
