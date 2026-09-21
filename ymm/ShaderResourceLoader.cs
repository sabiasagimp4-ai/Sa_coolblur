namespace SaCoolBlurYmm;
internal static class ShaderResourceLoader
{
    public static byte[] Get(string name)
    {
        using var stream = typeof(ShaderResourceLoader).Assembly.GetManifestResourceStream($"SaCoolBlurYmm.{name}.cso")
            ?? throw new InvalidOperationException($"Shader resource missing: {name}");
        using var memory = new System.IO.MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
