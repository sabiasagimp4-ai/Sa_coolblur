# Sa_coolblur

AEで使っていた自作プラグインを移植したものです。
YMM4用の色分散レンズブラー。ぼかしの広がりを3つの色に分け、玉ボケや色のにじみを作ります。

帯状・円形・深度マップ画像・全体の4モード。分散色、ボケの縁、ハイライト、縦横比、ピント位置を調整できます。

## インストール

[Actions](https://github.com/sabiasagimp4-ai/Sa_coolblur/actions) の成功した実行から `SaCoolBlurYmm` をダウンロードし、展開したフォルダを YMM4 の `user/plugin` に置いて再起動してください。映像エフェクトの「ぼかし」→「Sa_coolblur」で追加できます。

## パラメータ例

添付画像をコードで中央クロップし、896×504（正確な16:9）にした入力を使っています。YMM4版のシェーダーと同じ式をCPU側へ転記し、同じVogelサンプル点・リニアライト変換・色分散・ボケ縁の重みでレンダリングしました。画像生成は使っていません。

![コードでレンダリングしたSa_coolblurの16:9比較](assets/examples/comparison.jpg)

左上は原画、右上は帯状フォーカス、左下は円形フォーカス＋色分散、右下はアナモルフィックぼかしです。各パネルに実際に使った半径（R）、分散量（D）、ボケの縁（Edge）、ハイライト（Hi）、縦横比（A）を表示しています。

再生成用コードは [tools/render_examples.py](tools/render_examples.py) と [tools/render_reference.cpp](tools/render_reference.cpp)、全設定値は [parameters.json](assets/examples/parameters.json) にあります。

## 補足

- 深度マップはPNGなどの静止画を指定します。AEの別レイヤー・動画参照には未対応です。未指定ならぼかしません。
- マップ画像は素材全体に合わせて伸縮します。ピントマップ表示では白い部分ほど強くぼけます。
- ピント幅は帯状では帯の全幅、円形では半径です。中心は素材に対する割合です。
- 大きい半径で粒が見える場合は画質を上げてください。高品質は処理が重くなります。
- 深度マップを上書きしたときは、ファイルを選び直すかプロジェクトを開き直してください。

[移植元](https://github.com/sabiasagimp4-ai/cool_blur) ／ [移植範囲と検証](docs/PORT.md)
