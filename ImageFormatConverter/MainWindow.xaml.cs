using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageFormatConverter
{
    public partial class MainWindow : Window
    {
        //存储待转换的文件（2，1）
        private ObservableCollection<FileItem> _files = new ObservableCollection<FileItem>();

        //服务
        private ImageConvertService _service = new ImageConvertService();

        //输出路径
        private string _outputDirectory;

        public MainWindow()
        {
            InitializeComponent();
            LstFiles.ItemsSource = _files;

            //把桌面作为默认的输出路径并展示
            _outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ConvertedImages");
            TxtOutputDir.Text = _outputDirectory;

            //绑定质量滑块
            SldQuality.ValueChanged += (s, e) => TxtQuality.Text = $"{SldQuality.Value}";
        }

        #region 文件拖放与添加

        //文件拖放与添加，只是在窗口上拖放文件，而不是在列表框上拖放文件
        // 所以需要在窗口上添加一个事件处理方法
        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            //如果拖放的是文件
            // 则设置效果为复制
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                e.Effects = System.Windows.DragDropEffects.Copy;
            // 否则设置效果为无
            else
                e.Effects = System.Windows.DragDropEffects.None;
            //事件已处理
            e.Handled = true;
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            //如果拖放的是文件
            // 则添加到列表框
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                AddFiles(files);
            }
        }

        private void BtnAddFiles_Click(object sender, System.Windows.DragEventArgs e)
        {
            //打开文件对话框
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                // 多选
                Multiselect = true,
                //显示名字+扩展名
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.svg;*.gif;*.webp;*.tiff;*.pdf|所有文件|*.*"
            };

            //确认后添加文件
            if (dialog.ShowDialog() == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void AddFiles(string[] files)
        {
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                var supported = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".svg", ".gif", ".webp", ".tiff", ".pdf" };

                //如果文件是支持的格式且不在列表框中
                // 添加到列表框
                if (supported.Contains(ext) && !_files.Any(f => f.FilePath == file))
                {
                    _files.Add(new FileItem { FilePath = file });
                }
            }
            UpdateStatus();
        }

        //更新状态
        private void UpdateStatus()
        {
            TxtCount.Text = $"文件数:{_files.Count}";
            TxtStatus.Text = $"就绪|已加载{_files.Count}个文件";
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            //清除空列表框
            _files.Clear();
            //清空预览
            ImgPreview.Source = null;
            //清空预览提示
            TxtPreviewHint.Visibility = Visibility.Visible;
            //更新状态
            UpdateStatus();
        }

        #endregion 文件拖放与添加

        #region 预览与选择

        private async void LstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //获取当前选中的文件
            var item = LstFiles.SelectedItem as FileItem;
            if (item == null) return;
            try
            {
                //隐藏旧图
                ImgPreview.Source = null;

                TxtPreviewHint.Text = "加载中...";
                TxtPreviewHint.Visibility = Visibility.Visible;

                //创建位图对象
                var bmp = new BitmapImage();
                //初始化位图对象
                bmp.BeginInit();
                //设置图像源路径
                bmp.UriSource = new Uri(item.FilePath);
                //设置缓存选项
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                //设置缓存为加载时缓存
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                //结束初始化
                bmp.EndInit();

                //异步等待图像加载完成
                await WaitForImageLoaded(bmp);
                ImgPreview.Source = bmp;
                TxtPreviewHint.Visibility = Visibility.Collapsed;
            }
            catch (FileNotFoundException)
            {
                TxtPreviewHint.Text = "文件不存在";
            }
            catch (Exception ex)
            {
                TxtPreviewHint.Text = $"无法预览此文件:{ex.Message}";
            }
            finally
            {
                if (ImgPreview.Source == null)
                {
                    TxtPreviewHint.Visibility = Visibility.Visible;
                }
            }
        }

        private Task WaitForImageLoaded(BitmapImage image)
        {
            var tcs = new TaskCompletionSource<bool>();

            //如果图像下载完成
            if (image.IsDownloading == false)
            {
                tcs.SetResult(true);
                return tcs.Task;
            }

            //注册事件处理
            EventHandler handler = null;
            handler = (s, e) =>
            {
                //取消事件订阅
                image.DownloadCompleted -= handler;
                // 设置任务完成
                tcs.SetResult(true);
            };
            //订阅事件
            image.DownloadCompleted += handler;

            //失败
            image.DownloadFailed += (s, e) =>
            {
                //取消事件订阅
                image.DownloadCompleted -= handler;
                // 设置任务异常
                tcs.SetException(e.ErrorException);
            };
            return tcs.Task;
        }

        #endregion 预览与选择

        #region 设置与颜色选择

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            //选文件夹
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            //如果用户选择了文件夹
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //存储输出目录
                _outputDirectory = dialog.SelectedPath;
                //更新输出目录文本框
                TxtOutputDir.Text = _outputDirectory;
            }
        }

        //颜色选择按钮点击事件处理
        private void BtnPickColor_Click(object sender, RoutedEventArgs e)
        {
            //颜色选择框
            var dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //获取用户选择的颜色
                var color = dialog.Color;
                //将颜色转换为十六进制字符串
                string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                TxtNewColor.Text = hex;
                //更新颜色预览
                BorderColorPreview.Background = new SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
            }
        }

        //预览颜色按钮点击事件处理
        private void BtnPreviewColor_Click(object sender, RoutedEventArgs e)
        {
            //如果用户没有选择文件
            var item = LstFiles.SelectedItem as FileItem;
            if (item == null || !item.IsSvg)
            {
                System.Windows.MessageBox.Show("请先选择SVG的文件 !!!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                //读取SVG文件内容
                string svgContent = File.ReadAllText(item.FilePath);
                //获取当前转换选项
                var options = GetCurrentOptions();
                //处理SVG文件
                string processed = SvgColorEngine.ProcessSvg(svgContent, options);

                //显示处理后的SVG
                string tempFile = Path.GetTempFileName() + ".svg";
                //将处理后的SVG内容写入临时文件
                File.WriteAllText(tempFile, processed);

                //加载临时文件
                //创建位图对象
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(tempFile);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                ImgPreview.Source = bmp;

                //删除临时文件
                try { File.Delete(tempFile); }
                catch { }

                TxtStatus.Text = "正在预览替换后效果";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"预览失败:{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ConvertOptions GetCurrentOptions()
        {
            //获取用户选择的颜色替换模式
            var mode = (ColorReplaceMode)CmbColorMode.SelectedIndex;
            //创建转换选项对象
            return new ConvertOptions
            {
                //设置输出格式
                OutputFormat = ((ComboBoxItem)CmbOutputFormat.SelectedItem).Content.ToString(),
                //设置JPG质量
                JpegQuality = (int)SldQuality.Value,
                //设置是否保持原始尺寸
                KeepOriginalSize = ChkReplaceAllColors.IsChecked == true,
                //旧颜色
                OldColor = TxtOldColor.Text,
                //设置新颜色
                NewColor = TxtNewColor.Text,
                //设置是否替换所有颜色
                ReplaceAllColors = ChkReplaceAllColors.IsChecked == true,
                //设置颜色替换模式
                ReplaceMode = mode
            };
        }

        #endregion 设置与颜色选择
    }
}