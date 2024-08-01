using Microsoft.Gaming.XboxGameBar;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace XboxGamebarCrosshair
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //xbox widget
        private XboxGameBarWidget widget = null;
        //image
        private readonly BitmapImage bitmapImage = new BitmapImage();

        private static readonly string saveName = "saveImage.png";

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //初始化
            LoadSettings();
            widget = e.Parameter as XboxGameBarWidget;
            if (widget == null)
            {
                return;
            }
            //隐藏菜单
            widget.GameBarDisplayModeChanged += GameBarDisplayModeChanged;
            widget.MinWindowSize = new Size(390, 300);
            widget.MaxWindowSize = new Size(1300, 1000);
            widget.VerticalResizeSupported = false;
            widget.HorizontalResizeSupported = false;
            widget.ClickThroughEnabledChanged += Widget_ClickThroughEnabledChanged;
        }

        private async void Widget_ClickThroughEnabledChanged(XboxGameBarWidget sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (widget.ClickThroughEnabled == false)
                {
                    textBlock.Text = "Please enable the 'Click-Through' feature\n near by settings";
                }
                else
                {
                    textBlock.Text = "";
                }
            });
        }

        private async void LoadDefaultImage()
        {
            var uri = new Uri("ms-appx:///Assets/Circle-Crosshair.png");
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            LoadImage(file);
        }

        private async void LoadSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            //加载配置
            var ShiftYSetting = localSettings.Values["ShiftY"];
            if (ShiftYSetting != null)
            {
                var ShiftY = (double)ShiftYSetting;
                textBoxPosShiftY.Text = ShiftY.ToString();
            }
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            var saveFileInfo = await storageFolder.TryGetItemAsync(saveName);
            if (saveFileInfo != null)
            {
                var saveFile = await storageFolder.GetFileAsync(saveName);
                LoadImage(saveFile);
            }
            else
            {
                LoadDefaultImage();
            }
        }

        private async void SaveImage(StorageFile file)
        {
            using (var stream = await file.OpenReadAsync())
            {
                StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                var saveFileInfo = await storageFolder.TryGetItemAsync(saveName) ??
                    await storageFolder.CreateFileAsync(saveName,
                        CreationCollisionOption.ReplaceExisting);
                var saveFile = await storageFolder.GetFileAsync(saveName);
                using (var saveStream = await saveFile.OpenStreamForWriteAsync())
                {
                    stream.AsStream().CopyTo(saveStream);
                }
            }
        }

        private void SaveSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            // Save a setting locally on the device
            var success = double.TryParse(textBoxPosShiftY.Text, out double numberY);
            if (!success)
            {
                numberY = 0;
            }
            localSettings.Values["ShiftY"] = numberY;
        }

        private async void GameBarDisplayModeChanged(XboxGameBarWidget sender, object args)
        {
            // 请确保将此小部件分派到正确的UI线程，不能保证Game Bar事件会出现在同一线程中。
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                menuGrid.Visibility = sender.GameBarDisplayMode == XboxGameBarDisplayMode.PinnedOnly ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        private async void ResetImageButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDefaultImage();
            //delete save image
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            var saveFileInfo = await storageFolder.TryGetItemAsync(saveName);
            if (saveFileInfo != null)
            {
                await saveFileInfo.DeleteAsync();
            }
        }

        private async void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".png");

            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                LoadImage(file);
                //另存图像
                SaveImage(file);
            }
        }

        private async void LoadImage(StorageFile file)
        {
            using (var stream = await file.OpenReadAsync())
            {
                //渲染图像
                bitmapImage.SetSource(stream);
                CrosshairImage.Source = bitmapImage;
                CrosshairImage.Height = bitmapImage.PixelHeight;
                CrosshairImage.Width = bitmapImage.PixelWidth;
                ReFixsize();
            }
        }

        private async void SetPos_Click(object sender, RoutedEventArgs e)
        {
            ReFixsize();
            SaveSettings();
            if (widget != null)
            {
                await widget.CenterWindowAsync();
            }
        }

        private async void ReFixsize()
        {
            var success = double.TryParse(textBoxPosShiftY.Text, out double numberY);
            if (!success)
            {
                numberY = 0;
                textBoxPosShiftY.Text = "0";
            }
            //偏移图像
            var thinkess = CrosshairImage.Margin;
            thinkess.Top = numberY;
            CrosshairImage.Margin = thinkess;
            //计算能放下图像的高度
            var height = bitmapImage.PixelHeight + numberY + 10;
            height = height < 300 ? 300 : height;
            height = height > 1000 ? 1000 : height;
            //想要请求修改大小必须设置页面最小和最大值
            if (widget == null)
            {
                return;
            }
            //设置窗口大小
            var size = new Size(height * 1.3, height);//实际生效大小要乘缩放比例
            await widget.TryResizeWindowAsync(size);
        }

        private void textBoxPosShiftY_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textbox = (TextBox)sender;
            if (!Regex.IsMatch(textbox.Text, "^-?\\d*\\.?\\d*$") && textbox.Text != "")
            {
                int pos = textbox.SelectionStart - 1;
                textbox.Text = textbox.Text.Remove(pos, 1);
                textbox.SelectionStart = pos;
            }
        }
    }
}
