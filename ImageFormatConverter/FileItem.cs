using System.IO;

///<summary>
///描述文件信息
///</summary>
namespace ImageFormatConverter
{
    public class FileItem
    {
        //提取文件路径可读可写
        public string FilePath { get; set; }

        //提取文件名字
        public string FileName => Path.GetFileName(FilePath);

        //提取文件后缀名字然后改成全大写，不然后面还要判断
        public string Format => Path.GetExtension(FilePath).ToUpperInvariant();

        //判断是不是SVG决定功能开启
        public bool IsSvg => Format == ".SVG";
    }
}