using System.Text;

namespace InkFlow.Sources.Adapters.Kanunu8;

/// <summary>书源兼容层的编码支持:按需注册 CodePages 提供程序并缓存 GB18030。</summary>
public static class SourceEncodings
{
    private static Encoding? _gb18030;
    private static readonly object Lock = new();

    /// <summary>GB18030(兼容 GB2312/GBK 老站点)。</summary>
    public static Encoding Gb18030
    {
        get
        {
            if (_gb18030 is not null)
            {
                return _gb18030;
            }

            lock (Lock)
            {
                if (_gb18030 is null)
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    _gb18030 = Encoding.GetEncoding("GB18030");
                }
            }

            return _gb18030;
        }
    }
}
