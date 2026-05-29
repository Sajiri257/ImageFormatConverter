///<summary>
///转换配置
///</summary>
namespace ImageFormatConverter
{
    public class ConvertOptions
    {
        //默认输出PNG
        public string OutputFormat { get; set; } = "PNG";

        //默认JPG质量为100
        public int JpegQuality { get; set; } = 100;

        //默认保持原始尺寸
        public bool KeepOriginalSize { get; set; } = true;

        //KeepOriginalSize == false;
        //目标宽高
        public int TargetWidth { get; set; }

        public int TargetHeight { get; set; }

        //SVG专用
        //颜色替换默认红色，脸红的颜色
        public string OldColor { get; set; }

        public string NewColor { get; set; } = "#FF4777";

        public bool ReplaceAllColors { get; set; }

        public ColorReplaceMode ReplaceMode { get; set; } = ColorReplaceMode.FillOnly;
    }

    //枚举换什么地方的颜色
    public enum ColorReplaceMode
    {
        //只换填充色（我最常用）
        FillOnly,

        //只换描边色
        StrokeOnly,

        //描边加填充
        FillAndStroke,

        //我全都要
        Global
    }
}