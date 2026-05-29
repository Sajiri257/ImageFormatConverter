using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ImageFormatConverter
{
    ///<summary>
    ///SVG更换颜色
    ///</summary>
    public static class SvgColorEngine
    {
        //处理SVG的颜色替换
        public static string ProcessSvg(string svgContent, ConvertOptions options)
        {
            //先判断要不要处理
            if (!options.ReplaceAllColors && string.IsNullOrWhiteSpace(options.OldColor)) return svgContent;

            //把SVG换成XML
            var doc = XDocument.Parse(svgContent);
            var ns = doc.Root.GetDefaultNamespace();
            //拿所有标签
            var elements = doc.Descendants().ToList();

            //转小写去空格 给个默认的颜色
            string oldColor = options.OldColor?.Trim().ToLowerInvariant();
            string newColor = options.NewColor?.Trim() ?? "#FF4777";

            //遍历所有的标签 其实就做三件事
            foreach (var el in elements)
            {
                //换fill的颜色
                if (options.ReplaceMode == ColorReplaceMode.FillOnly || options.ReplaceMode == ColorReplaceMode.FillAndStroke)
                {
                    ReplaceAttribute(el, "fill", oldColor, newColor, options);
                }

                //换stroke的颜色
                if (options.ReplaceMode == ColorReplaceMode.StrokeOnly || options.ReplaceMode == ColorReplaceMode.FillAndStroke)
                {
                    ReplaceAttribute(el, "stroke", oldColor, newColor, options);
                }

                //全局替换包含style的颜色
                if (options.ReplaceMode == ColorReplaceMode.Global || options.ReplaceMode == ColorReplaceMode.FillAndStroke)
                {
                    var styleAttr = el.Attribute("style");
                    if (styleAttr != null)
                    {
                        styleAttr.Value = ReplaceColorsIncss(styleAttr.Value, oldColor, newColor);
                    }
                }
            }

            //处理style标签
            var styleElements = doc.Descendants().Where(e => e.Name.LocalName == "style");
            foreach (var styleEl in styleElements)
            {
                if (!string.IsNullOrWhiteSpace(styleEl.Value))
                {
                    styleEl.Value = ReplaceColorsIncss(styleEl.Value, oldColor, newColor);
                }
            }
            return doc.ToString(SaveOptions.DisableFormatting);
        }

        //更换标签属性
        private static void ReplaceAttribute(XElement el, string attrName, string oldColor, string newColor, ConvertOptions options)
        {
            var attr = el.Attribute(attrName);
            //不存在、没有颜色和透明的就不管它
            if (attr == null || attr.Value == "none" || attr.Value == "transparent") return;
            if (options.ReplaceAllColors || ColorMatches(attr.Value, oldColor))
            {
                attr.Value = newColor;
            }
        }

        //正则换颜色
        private static string ReplaceColorsIncss(string css, string oldColor, string newColor)
        {
            if (string.IsNullOrWhiteSpace(css)) return css;

            var colorPattern = @"#([0-9A-Fa-f]{3}){1,2}\b|rgb\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*\)|rgba\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*[\d.]+\s*\)";

            if (string.IsNullOrWhiteSpace(oldColor))
            {
                return Regex.Replace(css, colorPattern, newColor, RegexOptions.IgnoreCase);
            }
            else
            {
                return Regex.Replace(css, $@"(?<=[^-\w]){Regex.Escape(oldColor)}(?=[^-\w])", newColor, RegexOptions.IgnoreCase);
            }
        }

        //颜色匹配判断
        private static bool ColorMatches(string value, string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;
            //让FFF和#FFF相等然后比较
            return value.Trim().Equals(target, System.StringComparison.OrdinalIgnoreCase) || value.Trim().Equals(target.Replace("#", ""),
                System.StringComparison.OrdinalIgnoreCase);
        }
    }
}