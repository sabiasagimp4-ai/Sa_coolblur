using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
namespace SaCoolBlurYmm;

internal sealed class PrepareEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<PrepareEffect.Impl>(devices))
{
    public float Gamma { set => SetValue(0, value); }
    public float Pivot { set => SetValue(1, value); }
    public float Linear { set => SetValue(2, value); }
    public float Reserved { set => SetValue(3, value); }
    [CustomEffect(1)]
    private sealed class Impl : D2D1CustomShaderEffectImplBase<Impl>
    {
        private Constants _c;
        public Impl() : base(ShaderResourceLoader.Get("Prepare")) { }
        [CustomEffectProperty(PropertyType.Float, 0)] public float Gamma { get => _c.Gamma; set { _c.Gamma = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 1)] public float Pivot { get => _c.Pivot; set { _c.Pivot = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 2)] public float Linear { get => _c.Linear; set { _c.Linear = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 3)] public float Reserved { get => _c.Reserved; set { _c.Reserved = value; UpdateConstants(); } }
        protected override void UpdateConstants()
        {
            drawInformation?.SetOutputBuffer(BufferPrecision.PerChannel32Float, ChannelDepth.Four);
            drawInformation?.SetPixelShaderConstantBuffer(_c);
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Constants
        {
            public float Gamma, Pivot, Linear, Reserved;
        }
    }
}
