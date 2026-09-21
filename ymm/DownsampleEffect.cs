using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SaCoolBlurYmm;

/// <summary>
/// CUDA版のcontent-only box downsampleをDirect2Dの変換として再現する。
/// 出力の左上は入力と同じ座標に保ち、幅と高さだけをceil(size / factor)へ縮める。
/// </summary>
internal sealed class DownsampleEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<DownsampleEffect.Impl>(devices))
{
    public float Factor { set => SetValue(0, value); }

    [CustomEffect(1)]
    private sealed class Impl : D2D1CustomShaderEffectImplBase<Impl>
    {
        private Constants _c;
        private RawRect _sourceBounds, _outputBounds;

        public Impl() : base(ShaderResourceLoader.Get("Downsample")) { }

        [CustomEffectProperty(PropertyType.Float, 0)]
        public float Factor
        {
            get => _c.Factor;
            set
            {
                _c.Factor = Math.Clamp(float.IsFinite(value) ? value : 1, 1, 4);
                UpdateConstants();
            }
        }

        protected override void UpdateConstants()
        {
            drawInformation?.SetOutputBuffer(BufferPrecision.PerChannel32Float, ChannelDepth.Four);
            // The CUDA kernel averages exact source pixels, before the
            // low-resolution gather applies its own bilinear filtering.
            drawInformation?.SetInputDescription(0, new InputDescription { Filter = Filter.MinMagMipPoint, LevelOfDetailCount = 1 });
            drawInformation?.SetPixelShaderConstantBuffer(_c);
        }

        private int FactorInt => Math.Clamp((int)MathF.Floor(_c.Factor + .5f), 1, 4);

        public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
        {
            _sourceBounds = inputRects[0];
            int factor = FactorInt;
            int width = Math.Max(0, _sourceBounds.Right - _sourceBounds.Left);
            int height = Math.Max(0, _sourceBounds.Bottom - _sourceBounds.Top);
            _outputBounds = new RawRect(
                _sourceBounds.Left,
                _sourceBounds.Top,
                _sourceBounds.Left + (width + factor - 1) / factor,
                _sourceBounds.Top + (height + factor - 1) / factor);
            outputRect = _outputBounds;
            outputOpaqueSubRect = default;
            _c.SourceLeft = _sourceBounds.Left; _c.SourceTop = _sourceBounds.Top;
            _c.SourceRight = _sourceBounds.Right; _c.SourceBottom = _sourceBounds.Bottom;
            _c.OutputLeft = _outputBounds.Left; _c.OutputTop = _outputBounds.Top;
            _c.OutputRight = _outputBounds.Right; _c.OutputBottom = _outputBounds.Bottom;
            UpdateConstants();
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            int factor = FactorInt;
            var source = _sourceBounds;
            var output = _outputBounds;
            if (source.Right <= source.Left || source.Bottom <= source.Top || output.Right <= output.Left || output.Bottom <= output.Top)
            {
                inputRects[0] = outputRect;
                return;
            }

            int left = source.Left + (outputRect.Left - output.Left) * factor;
            int top = source.Top + (outputRect.Top - output.Top) * factor;
            int right = source.Left + (outputRect.Right - output.Left) * factor;
            int bottom = source.Top + (outputRect.Bottom - output.Top) * factor;
            inputRects[0] = new RawRect(
                Math.Clamp(left, source.Left, source.Right),
                Math.Clamp(top, source.Top, source.Bottom),
                Math.Clamp(right, source.Left, source.Right),
                Math.Clamp(bottom, source.Top, source.Bottom));
        }

        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect) => _outputBounds;

        [StructLayout(LayoutKind.Sequential)]
        private struct Constants
        {
            public float Factor, Reserved0, Reserved1, Reserved2;
            public float SourceLeft, SourceTop, SourceRight, SourceBottom;
            public float OutputLeft, OutputTop, OutputRight, OutputBottom;
        }
    }
}
