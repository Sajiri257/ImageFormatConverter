using System;
using System.IO;
using System.Linq;
using System.Windows;
using ImageMagick;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

///<summary>
///核心 本质是 输入 -> 修改 -> 输出
/// </summary>

namespace ImageFormatConverter
{
    public class ImageConvertService
    {
        //传入文件、设置、输出的文件夹路径
        public void Convert(FileItem file, ConvertOptions options, string outputDir)
        {
            //准备输出的路径
            //拿到配置的输出格式是什么
            string ext = options.OutputFormat.ToLowerInvariant();
            //拿源文件的名字换成我们要的后缀
            string outputName = Path.GetFileNameWithoutExtension(file.FileName) + "." + ext;
            //再把路径也拼接上去
            string outputPath = Path.Combine(outputDir, outputName);

            //路径不存在就搞一个
            Directory.CreateDirectory(outputDir);

            //SVG
            if (file.IsSvg)
            {
                ConvertSvg(file, outputPath, options);
                return;
            }

            //PDF
            if (ext == "pdf")
            {
                ConvertToPdf(file, outputPath, options);
                return;
            }

            //都不是
            using (var image = new MagickImage(file.FilePath))
            {
                //尺寸
                ApplyOptions(image, options);
                //格式
                image.Format = GetMagickFormat(ext);

                //如果是jpg或者jpeg就要设置质量
                if (ext == "jpg" || ext == "jpeg")
                {
                    image.Quality = (uint)options.JpegQuality;
                }

                //规定ico不能比256*256大，不然就缩小
                else if (ext == "ico")
                {
                    if (image.Width > 256 || image.Height > 256)
                    {
                        image.Resize(new MagickGeometry(256, 256) { IgnoreAspectRatio = false });
                    }
                }

                //保存文件
                image.Write(outputPath);
            }
        }

        //SVG专用方法
        private void ConvertSvg(FileItem file, string outputPath, ConvertOptions options)
        {
            //读取SVG的原文
            string svgContent = File.ReadAllText(file.FilePath);

            //到底要不要换颜色
            if (!string.IsNullOrWhiteSpace(options.NewColor) &&
                (options.ReplaceAllColors || !string.IsNullOrWhiteSpace(options.OldColor)))
            {
                svgContent = SvgColorEngine.ProcessSvg(svgContent, options);
            }

            if (options.OutputFormat.ToUpperInvariant() == "SVG")
            {
                File.WriteAllText(outputPath, svgContent);
                return;
            }

            using (var image = new MagickImage())
            {
                //读SVG的配置
                var readSettings = new MagickReadSettings
                {
                    //格式
                    Format = MagickFormat.Svg,
                    //背景颜色
                    BackgroundColor = MagickColors.Transparent,
                    Width = options.KeepOriginalSize ? null : (uint?)options.TargetWidth,
                    Height = options.KeepOriginalSize ? null : (uint?)options.TargetHeight
                };
                //渲染成图片

                image.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent)), readSettings);

                if (!options.KeepOriginalSize && options.TargetWidth > 0)
                {
                    //换大小
                    image.Resize((uint)options.TargetWidth, (uint)options.TargetHeight);
                }

                image.Format = GetMagickFormat(options.OutputFormat.ToLowerInvariant());
                //换JPG
                if (options.OutputFormat.ToUpperInvariant() == "JPG")
                {
                    image.Quality = (uint)options.JpegQuality;
                    image.BackgroundColor = MagickColors.White;
                    image.Alpha(AlphaOption.Remove);
                }

                image.Write(outputPath);
            }
        }

        //图片转PDF
        private void ConvertToPdf(FileItem file, string outputPath, ConvertOptions options)
        {
            //创建文档
            var document = new PdfDocument();
            //加一页
            var page = document.AddPage();
            //搞个画布
            var gfx = XGraphics.FromPdfPage(page);

            using (var image = new MagickImage(file.FilePath))
            {
                double pageWidth = page.Width.Point;
                double pageHeight = page.Height.Point;
                //缩小居中
                double ratio = Math.Min(pageWidth / image.Width, pageHeight / image.Height) * 0.9;
                int drawWidth = (int)(image.Width * ratio);
                int drawHeight = (int)(image.Height * ratio);

                double x = (pageWidth - drawWidth) / 2;
                double y = (pageHeight - drawHeight) / 2;

                //先用PNG因为不支持
                string tempPng = Path.GetTempFileName() + ".png";
                using (var tempImg = image.Clone())
                {
                    tempImg.Format = MagickFormat.Png;
                    tempImg.Write(tempPng);
                }

                var xImage = XImage.FromFile(tempPng);
                //居中
                gfx.DrawImage(xImage, x, y, drawWidth, drawHeight);
                xImage.Dispose();

                //把临时文件删了
                try { File.Delete(tempPng); } catch { }
            }

            document.Save(outputPath);
            document.Close();
        }

        //尺寸缩放
        private void ApplyOptions(MagickImage image, ConvertOptions options)
        {
            if (!options.KeepOriginalSize && options.TargetWidth > 0 && options.TargetHeight > 0)
            {
                image.Resize(new MagickGeometry((uint)options.TargetWidth, (uint)options.TargetHeight)
                {
                    //等比例
                    IgnoreAspectRatio = false,
                    //长边
                    Greater = true
                });
            }
        }

        private MagickFormat GetMagickFormat(string ext)
        {
            //根据后缀返回对应的格式 不认识的都给我搞成PNG
            return ext.ToLowerInvariant() switch
            {
                "png" => MagickFormat.Png,
                "jpg" or "jpeg" => MagickFormat.Jpeg,
                "bmp" => MagickFormat.Bmp,
                "ico" => MagickFormat.Ico,
                "gif" => MagickFormat.Gif,
                "webp" => MagickFormat.WebP,
                "tiff" or "tif" => MagickFormat.Tiff,
                _ => MagickFormat.Png
            };
        }
    }
}