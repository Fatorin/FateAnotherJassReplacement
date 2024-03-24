using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.AccessCache;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using FateAnotherJassReplacement.Models;
using Google.Protobuf;
using System.Reflection;
using FateAnotherJassReplacement.Utility;

namespace FateAnotherJassReplacement
{
    public sealed partial class MainPage : Page
    {
        const string JassFileNewLine = "\n";
        const string LastSavedFileKey = "last_saved_file";
        const string ConfigFileName = "config.dat";
        readonly ObservableCollection<ConfigItem> DataConfigs = new ObservableCollection<ConfigItem>();
        string fileContent = string.Empty;
        string filePath = string.Empty;
        int lines = 1;
        int position = -1;

        public MainPage()
        {
            this.InitializeComponent();
            var size = new Size(1280, 720);
            ApplicationView.PreferredLaunchViewSize = size;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            Window.Current.CoreWindow.SizeChanged += (s, e) =>
            {
                ApplicationView.GetForCurrentView().TryResizeView(size);
            };
            LoadConfig();
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(fileContent))
            {
                return;
            }

            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "war3map"
            };
            savePicker.FileTypeChoices.Add("jass", new List<string>() { ".j" });
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                CachedFileManager.DeferUpdates(file);
                await FileIO.WriteTextAsync(file, fileContent);
                Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    LogOutput("檔案 " + file.Name + " 已儲存");
                    var mru = StorageApplicationPermissions.MostRecentlyUsedList;
                    ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                    localSettings.Values[LastSavedFileKey] = mru.Add(file);
                }
                else
                {
                    LogOutput("檔案 " + file.Name + " 無法儲存");
                }
            }
            else
            {
                LogOutput("已取消選擇");
            }
        }

        private void ConfigSaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigComboBox.SelectedIndex == -1)
            {
                LogOutput("請選擇一個組態。");
                return;
            }

            DataConfigs[ConfigComboBox.SelectedIndex].Lines = lines;
            DataConfigs[ConfigComboBox.SelectedIndex].SearchText = SearchTextBox.Text;
            DataConfigs[ConfigComboBox.SelectedIndex].Datas = ByteString.CopyFromUtf8(ReplaceTextBox.Text);
            SaveConfig();
            LogOutput("組態更新完成。");
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigComboBox.SelectedIndex == -1)
            {
                return;
            }

            DataConfigs.RemoveAt(ConfigComboBox.SelectedIndex);
            SaveConfig();
            LogOutput("刪除完成。");
        }

        private void ReplaceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(fileContent))
            {
                LogOutput("尚未讀取檔案。");
                return;
            }

            if (string.IsNullOrEmpty(SourceTextBox.Text) || string.IsNullOrEmpty(ReplaceTextBox.Text))
            {
                LogOutput("需要有來源字串以及替換字串。");
                return;
            }

            fileContent = fileContent.Replace(SourceTextBox.Text.ReplaceToUnixNewLine(), ReplaceTextBox.Text.ReplaceToUnixNewLine());
            LogOutput("替換完成。");
        }

        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(fileContent))
            {
                ContentDialog replaceDialog = new ContentDialog
                {
                    Title = "確認",
                    Content = "是否要取代現有資料？",
                    PrimaryButtonText = "是",
                    CloseButtonText = "否"
                };

                ContentDialogResult result = await replaceDialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    LogOutput("不進行讀取。");
                    return;
                }
            }

            FileOpenPicker picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".j");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }
            fileContent = (await FileIO.ReadTextAsync(file)).ReplaceToUnixNewLine();
            filePath = file.Path;
            LogOutput($"讀取檔案成功。{filePath}");
        }

        private void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateContent();
        }

        private void IncreaseLines_Click(object sender, RoutedEventArgs e)
        {
            lines += 1;
            UpdateLines();
        }

        private void ReduceLines_Click(object sender, RoutedEventArgs e)
        {
            if (lines == 1)
            {
                return;
            }

            lines -= 1;
            UpdateLines();
        }

        void UpdateLines()
        {
            UpdateLinesText();
            if (string.IsNullOrEmpty(fileContent))
            {
                return;
            }

            if (string.IsNullOrEmpty(SearchTextBox.Text))
            {
                return;
            }

            UpdateContent();
        }

        void UpdateLinesText()
        {
            LinesTextBlock.Text = lines.ToString();
        }

        void UpdateContent()
        {
            if (string.IsNullOrEmpty(fileContent))
            {
                LogOutput("請先讀取檔案再執行。");
                return;
            }

            if (string.IsNullOrEmpty(SearchTextBox.Text))
            {
                LogOutput("請輸入要搜尋的文字。");
                return;
            }

            var index = fileContent.IndexOf(SearchTextBox.Text, StringComparison.Ordinal);
            if (index == -1)
            {
                LogOutput("沒有找到文字。");
                return;
            }

            var startIndex = fileContent.LastIndexOf(JassFileNewLine, index);
            var endIndex = fileContent.IndexOf(JassFileNewLine, index + SearchTextBox.Text.Length);
            for (int i = 1; i < lines && endIndex != -1; i++)
            {
                endIndex = fileContent.IndexOf(JassFileNewLine, endIndex + JassFileNewLine.Length);
            }

            if (startIndex == -1)
            {
                LogOutput($"無法找到符合的文字。");
                return;
            }

            if (endIndex == -1)
            {
                if (lines > 1)
                {
                    LogOutput($"無法找到符合的文字，請把行數範圍縮小。");
                }
                else
                {
                    LogOutput($"無法找到符合的文字。");
                }
                return;
            }

            var str = fileContent.Substring(startIndex + JassFileNewLine.Length, endIndex - startIndex - JassFileNewLine.Length);
            SourceTextBox.Text = str;
            if (string.IsNullOrEmpty(ReplaceTextBox.Text))
            {
                ReplaceTextBox.Text = str;
            }
        }

        void LogOutput(string message)
        {
            LogTextBlock.Text += $"[{DateTime.Now}] - {message + Environment.NewLine}";
            LogScrollView.ChangeView(0, double.MaxValue, 1);
        }

        private void ConfigComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Text))
            {
                LogOutput($"請輸入正確的文字");
                return;
            }

            var config = DataConfigs.SingleOrDefault(v => v.Name == args.Text);
            if (config == null)
            {
                config = new ConfigItem
                {
                    Name = args.Text,
                    Lines = lines,
                    SearchText = SearchTextBox.Text,
                    Datas = ByteString.CopyFromUtf8(ReplaceTextBox.Text)
                };
                DataConfigs.Add(config);
            }
            else
            {
                if (ConfigComboBox.SelectedIndex == position)
                {
                    return;
                }
                lines = config.Lines;
                SearchTextBox.Text = config.SearchText;
                ReplaceTextBox.Text = config.Datas.ToStringUtf8();
            }

            ConfigComboBox.SelectedIndex = DataConfigs.IndexOf(config);
            position = ConfigComboBox.SelectedIndex;
            UpdateLinesText();
            SaveConfig();
            UpdateContent();
            args.Handled = true;
        }

        async void LoadConfig()
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.GetFileAsync(ConfigFileName);
                using (Stream fileStream = await file.OpenStreamForReadAsync())
                {
                    ConfigItems configs = ConfigItems.Parser.ParseFrom(fileStream);
                    DataConfigs.Clear();
                    foreach (var config in configs.ConfigItems_)
                    {
                        DataConfigs.Add(config);
                    }
                }
                LogOutput($"組態成功讀取。");
            }
            catch
            {
                LogOutput($"組態檔案 {ConfigFileName} 不存在。");
            }
        }

        async void SaveConfig()
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    var configs = new ConfigItems();
                    configs.ConfigItems_.AddRange(DataConfigs.ToList());
                    StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                    StorageFile file = await localFolder.CreateFileAsync(ConfigFileName, CreationCollisionOption.ReplaceExisting);

                    using (Stream fs = await file.OpenStreamForWriteAsync())
                    {
                        configs.WriteTo(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                LogOutput("無法儲存檔案：" + ex.Message);
            }
        }
    }
}
