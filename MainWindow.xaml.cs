using System.Diagnostics;
using System.Windows;
using System;
using FFMpegCore;
using FFMpegCore.Enums;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Linq;
using System.Threading;
using System.Net;
using Downloader;
using System.Windows.Threading;
using System.Text.RegularExpressions;

namespace WannaCriCS
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public enum CodecType
        {
            VP9 = 0,
            H264 = 1
        }

        public enum AnimationType
        {
            Off = 0,
            On = 1,
            Half = 2
        }

        public enum SnackBarType
        {
            Success = 0,
            ConvertError = 1,
            InputOutputError = 2,
            LinkError = 3,
            NotValid = 4,
        }

        public enum ProgramVideoState
        {
            None = 0,
            DownloadingVideo = 1,
            DownloadedVideo = 2,
            ConvertingVideo = 3,
            WaitforWannaCri = 4,
            RenameFile = 5
        }

        public enum ProgramAudioState
        {
            None = 0,
            DownloadingAudio = 1,
            DownloadedAudio = 2,
            ConvertingAudio = 3,
            NoAudio = 999
        }

        public ProgramVideoState CurrentVideoState = ProgramVideoState.None;

        public ProgramAudioState CurrentAudioState = ProgramAudioState.None;

        public CodecType TargetCodecType = CodecType.VP9;

        public string Suffix;

        public string InputName = string.Empty;

        public string OutputName = string.Empty;

        public string SafeOutputName = "converttempfile";

        public string OutputPath;

        public string OutputFileName;

        public StreamManifest streamManifest;

        public IVideoStreamInfo videoStreamInfo;

        public IStreamInfo audioStreamInfo;

        public class USMKey
        {
            public string Key = string.Empty;

            public bool UseKey => !string.IsNullOrEmpty(Key);

            public Regex regex = new Regex(@"^[A-Za-z0-9]+$");

            public bool KeyVaild => UseKey ? Key.StartsWith("0x") && Key.Length == 18 && regex.IsMatch(Key) : true;

        }

        public USMKey Key;

        public class MediaInfo
        {
            public string VideoCodec;

            public string VideoCodecThreeShort => VideoCodec.Substring(0, 3);

            public long VideoBitRate;

            public string AudioCodec;

            public string _MediaInfo;

            public string VideoTitle;

            public int maxLength = 23;

            public Regex regex = new Regex("[\\/:*?\"\'|<>]");

            public string VideoSafeTitle => regex.Replace(VideoTitle, string.Empty);

            public string ShortVideoTitle => VideoTitle.Length > maxLength ? VideoTitle.Substring(0, maxLength - 13) + "...(Hover to see more)" : VideoTitle;

            public TimeSpan VideoDuration;

            public CodecType _VideoCodec;

            public bool IsAudioAvailable;

        }

        public MediaInfo CurrentMedia;

        public string CRF;

        public string Volume;

        public string Brightness;

        public double VideoProgress;

        public double VideoDownloadProgress;

        public double AudioProgress;

        public double AudioDownloadProgress;

        public double LastDownloadProgress;

        public DateTime LastUpdateTime;

        public DateTime _LastUpdateTime;

        public enum WorkState
        {
            Online = 0,
            Local = 1,
            ExtractUSM = 2
        }

        public WorkState CurrentWorkState;

        public bool PythonCompleted;
        //AudioDownloadProgress 10% VideoDownloadProgress 35% AudioProgress 5% VideoProgress 45% Python 3% Rename 2%
        //AudioProgress 10% VideoProgress 80% Python 6% Rename 4%

        public bool ProcessLock;

        public static readonly DownloadConfiguration downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 8, // file parts to download, default value is 1
            MaxTryAgainOnFailover = 10,
            ParallelDownload = true, // download parts of file as parallel or not. Default value is false
            RequestConfiguration =
                {
                    Proxy = WebProxy.GetDefaultProxy(),
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0"
                }

        };
        public static readonly DownloadService downloader = new DownloadService(downloadOpt);

        public static readonly YoutubeClient Youtube = new YoutubeClient();

        public static readonly SolidColorBrush ThemeColor = new SolidColorBrush(Color.FromArgb(255, 128, 185, 238));

        public static readonly SolidColorBrush SuccessColor = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80));

        public static readonly SolidColorBrush ErrorColor = new SolidColorBrush(Color.FromArgb(255, 244, 54, 80));

        public static readonly ThicknessAnimation SlideOut = new ThicknessAnimation();

        public static readonly DoubleAnimation FadeOut = new DoubleAnimation();

        public static readonly ThicknessAnimation SlideIn = new ThicknessAnimation();

        public static readonly DoubleAnimation FadeIn = new DoubleAnimation();

        public static readonly DoubleAnimation ListOut = new DoubleAnimation();

        public static readonly DoubleAnimation ListIn = new DoubleAnimation();

        public static readonly DoubleAnimation ListHalf = new DoubleAnimation();

        public static readonly DoubleAnimation PanelOut = new DoubleAnimation();

        public static readonly DoubleAnimation PanelIn = new DoubleAnimation();

        public MainWindow()
        {
            InitializeComponent();

            InitAnimation();

            downloader.DownloadProgressChanged += OnDownloadProgressChanged;

            InitFFmpeg();

            CurrentMedia = new MediaInfo();
            Key = new USMKey();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            OnExitClicked(null, null);
        }

        public void Exit(object sender, EventArgs e)
        {
            var i = Process.GetProcessesByName("ffmpeg").Length - 1;
            while (i >= 0)
            {
                try
                {
                    Process.GetProcessesByName("ffmpeg")[i].Kill();
                }
                catch (Exception)
                {
                    if (i > 0)
                    {
                        i--;
                    }
                    else { Process.GetCurrentProcess().Kill(); }
                }
            }
            Process.GetCurrentProcess().Kill();

        }

        public void InitAnimation()
        {
            SlideOut.To = new Thickness(1000, 0, 0, 0); ;
            SlideOut.Duration = TimeSpan.FromSeconds(1);
            SlideOut.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseIn,
                Amplitude = 1,
            };

            SlideIn.To = new Thickness(0, 0, 0, 0);
            SlideIn.Duration = TimeSpan.FromSeconds(1);
            SlideIn.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 0.2,
            };

            FadeOut.To = 0;
            FadeOut.Duration = TimeSpan.FromSeconds(0.5);

            FadeIn.To = 1;
            FadeIn.Duration = TimeSpan.FromSeconds(1);

            ListOut.To = 120;
            ListOut.Duration = TimeSpan.FromSeconds(0.75);
            ListOut.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 1
            };

            ListIn.To = 0;
            ListIn.Duration = TimeSpan.FromSeconds(0.75);
            ListIn.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseIn,
                Amplitude = 1
            };

            ListHalf.To = 33;
            ListHalf.Duration = TimeSpan.FromSeconds(0.75);
            ListHalf.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseInOut,
                Amplitude = 1
            };

            PanelOut.To = 210;
            PanelOut.Duration = TimeSpan.FromSeconds(0.75);
            PanelOut.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 0.5
            };

            PanelIn.To = 0;
            PanelIn.Duration = TimeSpan.FromSeconds(0.75);
            PanelIn.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseIn,
                Amplitude = 0.5
            };
        }

        public void InitFFmpeg()
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = Environment.CurrentDirectory + "\\FFmpeg");
            GlobalFFOptions.Configure(options => options.Encoding = System.Text.Encoding.UTF8);
        }

        public void ListAnimation(AnimationType i)
        {
            switch (i)
            {
                case AnimationType.Off:
                    UIVideoList.BeginAnimation(HeightProperty, ListIn);
                    ListGrid.BeginAnimation(HeightProperty, ListIn);
                    OpenListButton.BeginAnimation(OpacityProperty, FadeOut);
                    OpenListButton.BeginAnimation(MarginProperty, SlideOut);
                    RecommendHint.BeginAnimation(OpacityProperty, FadeOut);
                    LinuxHint.BeginAnimation(OpacityProperty, FadeOut);
                    UIVideoList.IsEnabled = false;
                    break;
                case AnimationType.On:
                    UIVideoList.BeginAnimation(HeightProperty, ListOut);
                    UIVideoList.BeginAnimation(MarginProperty, SlideIn);
                    UIVideoList.BeginAnimation(OpacityProperty, FadeIn);
                    ListGrid.BeginAnimation(HeightProperty, ListOut);
                    OpenListButton.BeginAnimation(OpacityProperty, FadeOut);
                    OpenListButton.BeginAnimation(MarginProperty, SlideOut);
                    RecommendHint.BeginAnimation(OpacityProperty, FadeIn);
                    LinuxHint.BeginAnimation(OpacityProperty, FadeIn);
                    UIVideoList.IsEnabled = true;
                    break;
                case AnimationType.Half:
                    UIVideoList.BeginAnimation(HeightProperty, ListHalf);
                    UIVideoList.BeginAnimation(MarginProperty, SlideOut);
                    UIVideoList.BeginAnimation(OpacityProperty, FadeOut);
                    ListGrid.BeginAnimation(HeightProperty, ListHalf);
                    OpenListButton.BeginAnimation(OpacityProperty, FadeIn);
                    OpenListButton.BeginAnimation(MarginProperty, SlideIn);
                    RecommendHint.BeginAnimation(OpacityProperty, FadeOut);
                    LinuxHint.BeginAnimation(OpacityProperty, FadeOut);
                    UIVideoList.IsEnabled = false;
                    break;
            }
        }

        public void LockUI(bool isLocked = true)
        {
            if (isLocked)
            {
                InputSelect.IsEnabled = false; OutputSelect.IsEnabled = false;
                UrlText.IsEnabled = false; UrlSelect.IsEnabled = false; UrlClear.IsEnabled = false;
                LocalRadio.IsEnabled = false; OnlineRadio.IsEnabled = false; LocalUSMRadio.IsEnabled = false;
                VP9Radio.IsEnabled = false; H264Radio.IsEnabled = false;
                CRFBox.IsEnabled = false; VolumeBox.IsEnabled = false; BrightnessBox.IsEnabled = false;
                ConvertButton.IsEnabled = false;
                ListAnimation(AnimationType.Off);
                ProcessLock = true;

            }
            else
            {
                InputSelect.IsEnabled = true; OutputSelect.IsEnabled = true;
                UrlText.IsEnabled = true; UrlSelect.IsEnabled = true; UrlClear.IsEnabled = true;
                VP9Radio.IsEnabled = true; H264Radio.IsEnabled = true;
                CRFBox.IsEnabled = true; VolumeBox.IsEnabled = true; BrightnessBox.IsEnabled = true;
                ConvertButton.IsEnabled = true;
                ProcessLock = false;
                if (OnlineRadio.IsChecked == true)
                {
                    LocalRadio.IsEnabled = true;
                    LocalUSMRadio.IsEnabled = true;
                }
                else if (LocalRadio.IsChecked == true)
                {
                    OnlineRadio.IsEnabled = true;
                    LocalUSMRadio.IsEnabled = true;
                }
                else if (LocalUSMRadio.IsChecked == true)
                {
                    LocalRadio.IsEnabled = true;
                    OnlineRadio.IsEnabled = true;
                }
            }
        }

        public void SnackBarManager(SnackBarType snackBarType)
        {
            switch (snackBarType)
            {
                case SnackBarType.Success:
                    ErrorBar.Content = "Convert Completed.";
                    ErrorBar.Appearance = Wpf.Ui.Common.ControlAppearance.Success;
                    ErrorBar.Icon = Wpf.Ui.Common.SymbolRegular.CheckmarkCircle48;
                    ErrorBar.Timeout = 30000;
                    ErrorBar.Show();
                    LockUI(false);
                    break;
                case SnackBarType.ConvertError:
                    ErrorBar.Content = "An unexpected error has occurred.";
                    ErrorBar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
                    ErrorBar.Icon = Wpf.Ui.Common.SymbolRegular.ErrorCircle24;
                    ErrorBar.Timeout = -1;
                    ErrorBar.Show();
                    LockUI(false);
                    break;
                case SnackBarType.InputOutputError:
                    ErrorBar.Content = "Input or Output is empty. Or invaild Key";
                    ErrorBar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
                    ErrorBar.Icon = Wpf.Ui.Common.SymbolRegular.ErrorCircle24;
                    ErrorBar.Timeout = 2000;
                    ErrorBar.Show();
                    break;
                case SnackBarType.LinkError:
                    ErrorBar.Content = "Invalid link or network error.";
                    ErrorBar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
                    ErrorBar.Icon = Wpf.Ui.Common.SymbolRegular.ErrorCircle24;
                    ErrorBar.Timeout = 6000;
                    ErrorBar.Show();
                    break;
                case SnackBarType.NotValid:
                    ErrorBar.Content = "Invalid video file.";
                    ErrorBar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
                    ErrorBar.Icon = Wpf.Ui.Common.SymbolRegular.ErrorCircle24;
                    ErrorBar.Timeout = 6000;
                    ErrorBar.Show();
                    break;
            }
        }

        public async void CheckYoutubeLink()
        {
            ClearInfo();
            info.Text = "Getting Info...";
            var VideoUrl = UrlText.Text;
            try
            {
                var video = await Youtube.Videos.GetAsync(VideoUrl);
                CurrentMedia.VideoTitle = video.Title;
                CurrentMedia.VideoDuration = (TimeSpan)video.Duration;
                info.Text = "Title:" + CurrentMedia.ShortVideoTitle + " Duration:" + CurrentMedia.VideoDuration.ToString("hh\\:mm\\:ss");
                info.ToolTip = CurrentMedia.VideoTitle;
                info2.Text = "Getting Stream Info...";
                streamManifest = await Youtube.Videos.Streams.GetManifestAsync(VideoUrl);
                var VideoList = streamManifest.GetVideoOnlyStreams().Where(s => s.VideoCodec.Substring(0, 3) == "vp9" || s.VideoCodec.Substring(0, 3) == "avc").Where(s => s.VideoResolution.Height >= 0).ToList();
                foreach (var item in VideoList)
                {
                    UIVideoList.Items.Add(item.VideoQuality.Label + " " + item.VideoCodec + " " + item.Size.MegaBytes.ToString("0.00") + "MB");
                }
                info2.Text = "Waiting for select stream...";
                ListAnimation(AnimationType.On);
            }
            catch (Exception)
            {
                SnackBarManager(SnackBarType.LinkError);
                info.Text = "NotAvailable";
                CurrentMedia.VideoCodec = string.Empty;

            }
        }

        public void SetYoutubeInfo(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var Selected = UIVideoList.SelectedItem.ToString().Split(' ');
                var q = Selected[0];
                var c = Selected[1];
                videoStreamInfo = streamManifest.GetVideoOnlyStreams().Where(s => s.VideoCodec == c).Where(s => s.VideoQuality.Label == q).GetWithHighestVideoQuality();
                var VideoResolution = videoStreamInfo.VideoResolution.Width.ToString() + "x" + videoStreamInfo.VideoResolution.Height.ToString();
                var VideoFrameRate = videoStreamInfo.VideoQuality.Framerate.ToString();
                CurrentMedia.VideoCodec = videoStreamInfo.VideoCodec;
                CurrentMedia.VideoBitRate = videoStreamInfo.Bitrate.BitsPerSecond;
                Debug.WriteLine(CurrentMedia.VideoBitRate);
                RecommendCodec();
                info2.Text = "Video:" + VideoResolution + " " + CurrentMedia.VideoCodec + " Audio:Getting...";
                ListAnimation(AnimationType.Half);
                try
                {
                    audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    CurrentMedia.AudioCodec = audioStreamInfo.Bitrate.KiloBitsPerSecond.ToString("0");
                    info2.Text = "Video:" + VideoResolution + " " + VideoFrameRate + "fps " + CurrentMedia.VideoCodec + " Audio:" + CurrentMedia.AudioCodec + "Kbit/s";
                    CurrentMedia.IsAudioAvailable = true;
                }
                catch (Exception)
                {
                    CurrentMedia.IsAudioAvailable = false;
                }
            }
            catch (Exception)
            {
                ClearInfo();
            }


        }

        public async void SetMediaInfo()
        {
            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(InputName);
                CurrentMedia.VideoCodec = mediaInfo.PrimaryVideoStream.CodecName;
                CurrentMedia.VideoBitRate = mediaInfo.PrimaryVideoStream.BitRate;
                CurrentMedia.VideoDuration = mediaInfo.Duration;
                InputText.Text = InputName;
                try
                {
                    if (mediaInfo.PrimaryAudioStream.CodecName != null)
                    {
                        CurrentMedia.AudioCodec = mediaInfo.PrimaryAudioStream.CodecName;
                    }
                    if (CurrentMedia.AudioCodec != null && CurrentMedia.AudioCodec != "" && CurrentMedia.AudioCodec != "NotAvailable")
                    {
                        CurrentMedia.IsAudioAvailable = true;
                    }
                }
                catch (Exception)
                {
                    CurrentMedia.AudioCodec = "NotAvailable";
                    CurrentMedia.IsAudioAvailable = false;
                }
                RecommendCodec();
                info.Text = "Video:" + CurrentMedia.VideoCodec + " Audio:" + CurrentMedia.AudioCodec + " Duration:" + CurrentMedia.VideoDuration.ToString("hh\\:mm\\:ss");
            }
            catch (Exception)
            {
                InputName = InputText.Text;
                SnackBarManager(SnackBarType.NotValid);
                RecommendCodec();
            }
        }

        public string SelectInputFile()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            var result = openFileDialog.ShowDialog();

            if (result == true)
            {
                return openFileDialog.FileName;
            }
            else
            {
                return InputName;
            }
        }

        public string SelectUSMInputFile()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.FileName = "Output";
            openFileDialog.DefaultExt = ".usm";
            openFileDialog.Filter = "Criware Video File (.usm)|*.usm";
            openFileDialog.AddExtension = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            var result = openFileDialog.ShowDialog();

            if (result == true)
            {
                return openFileDialog.FileName;
            }
            else
            {
                return InputName;
            }
        }

        public string SelectOutputFile()
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = "Output";
            saveFileDialog.DefaultExt = ".usm";
            saveFileDialog.Filter = "Criware Video File (.usm)|*.usm";
            saveFileDialog.AddExtension = true;
            saveFileDialog.CheckFileExists = false;
            saveFileDialog.CheckPathExists = true;
            var result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                return saveFileDialog.FileName;
            }
            else
            {
                return OutputName;
            }
        }

        public void ClearInfo()
        {
            try
            {
                ListAnimation(AnimationType.Off);
                info.Text = "Waiting...";
                info2.Text = string.Empty;
                UIState.Text = string.Empty;
                UIState2.Text = string.Empty;
                VideoDownloadProgress = 0;
                AudioDownloadProgress = 0;
                VideoProgress = 0;
                AudioProgress = 0;
                UIVideoList.Items.Clear();
                CurrentMedia.VideoTitle = string.Empty;
                streamManifest = null;
                videoStreamInfo = null;
                audioStreamInfo = null;
                CurrentMedia._MediaInfo = string.Empty;
                CurrentMedia.VideoCodec = string.Empty;
                CurrentMedia.AudioCodec = string.Empty;
                CurrentMedia.IsAudioAvailable = false;
                RecommendCodec();
            }
            catch (Exception)
            {
                SnackBarManager(SnackBarType.ConvertError);
            }

        }

        public void OnUrlClearClicked(object sender, EventArgs e)
        {
            ClearInfo();
            UrlText.Text = string.Empty;
        }
        public void OnExitClicked(object sender, EventArgs e)
        {
            if (ProcessLock)
            {
                ExitDialog.Show();
            }
            else
            {
                Exit(null, null);
            }
        }

        public void OnCancelClicked(object sender, EventArgs e)
        {
            ExitDialog.Hide();
            ConfirmationDialog.Hide();
        }

        public void OnOnlineRadioClicked(object sender, RoutedEventArgs e)
        {
            CurrentWorkState = WorkState.Online;
            OnlineInput.BeginAnimation(MarginProperty, SlideIn);
            OnlineInput.BeginAnimation(OpacityProperty, FadeIn);
            LocalInput.BeginAnimation(MarginProperty, SlideOut);
            LocalInput.BeginAnimation(OpacityProperty, FadeOut);
            VideoSettingPanel.BeginAnimation(HeightProperty, PanelOut);
            TheWindow.MinWidth = 570;
            TheWindow.MinHeight = 570;
            //Codec.BeginAnimation(HeightProperty, CodecListIn);
            UrlText.IsEnabled = true;
            UrlSelect.IsEnabled = true;
            InputSelect.IsEnabled = false;
            OnlineRadio.IsEnabled = false;
            //OnlineListRadio.IsEnabled = true;
            LocalRadio.IsEnabled = true;
            LocalUSMRadio.IsEnabled = true;
            ClearInfo();
        }

        //public void OnOnlineListRadioClicked(object sender, RoutedEventArgs e)
        //{
        //    OnlineInput.BeginAnimation(MarginProperty, SlideIn);
        //    OnlineInput.BeginAnimation(OpacityProperty, FadeIn);
        //    LocalInput.BeginAnimation(MarginProperty, SlideOut);
        //    LocalInput.BeginAnimation(OpacityProperty, FadeOut);
        //    //Codec.BeginAnimation(HeightProperty, CodecListIn);
        //    UrlText.IsEnabled = true;
        //    UrlSelect.IsEnabled = true;
        //    InputSelect.IsEnabled = false;
        //    OnlineRadio.IsEnabled = true;
        //    //OnlineListRadio.IsEnabled = false;
        //    LocalRadio.IsEnabled = true;
        //    LocalUSMRadio.IsEnabled = true;
        //    ClearInfo();
        //}

        public void OnLocalRadioClicked(object sender, RoutedEventArgs e)
        {
            CurrentWorkState = WorkState.Local;
            OnlineInput.BeginAnimation(MarginProperty, SlideOut);
            OnlineInput.BeginAnimation(OpacityProperty, FadeOut);
            LocalInput.BeginAnimation(MarginProperty, SlideIn);
            LocalInput.BeginAnimation(OpacityProperty, FadeIn);
            InputSelect.BeginAnimation(HeightProperty, ListHalf);
            InputSelect.BeginAnimation(OpacityProperty, FadeIn);
            InputUSMSelect.BeginAnimation(HeightProperty, ListIn);
            InputUSMSelect.BeginAnimation(OpacityProperty, FadeOut);
            VideoSettingPanel.BeginAnimation(HeightProperty, PanelOut);
            TheWindow.MinHeight = 570;
            TheWindow.MinWidth = 570;
            //Codec.BeginAnimation(HeightProperty, CodecListOut);
            UrlText.IsEnabled = false;
            UrlSelect.IsEnabled = false;
            InputSelect.IsEnabled = true;
            InputUSMSelect.IsEnabled = false;
            OnlineRadio.IsEnabled = true;
            //OnlineListRadio.IsEnabled = true;
            LocalRadio.IsEnabled = false;
            LocalUSMRadio.IsEnabled = true;
            ClearInfo();
            InputText.Text = string.Empty;
        }

        public void OnLocalUSMRadioClicked(object sender, RoutedEventArgs e)
        {
            CurrentWorkState = WorkState.ExtractUSM;
            OnlineInput.BeginAnimation(MarginProperty, SlideOut);
            OnlineInput.BeginAnimation(OpacityProperty, FadeOut);
            LocalInput.BeginAnimation(MarginProperty, SlideIn);
            LocalInput.BeginAnimation(OpacityProperty, FadeIn);
            InputSelect.BeginAnimation(HeightProperty, ListIn);
            InputSelect.BeginAnimation(OpacityProperty, FadeOut);
            InputUSMSelect.BeginAnimation(HeightProperty, ListHalf);
            InputUSMSelect.BeginAnimation(OpacityProperty, FadeIn);
            VideoSettingPanel.BeginAnimation(HeightProperty, PanelIn);
            TheWindow.MinHeight = 380;
            TheWindow.MinWidth = 400;
            //Codec.BeginAnimation(HeightProperty, CodecListOut);
            UrlText.IsEnabled = false;
            UrlSelect.IsEnabled = false;
            InputSelect.IsEnabled = false;
            InputUSMSelect.IsEnabled = true;
            OnlineRadio.IsEnabled = true;
            //OnlineListRadio.IsEnabled = true;
            LocalRadio.IsEnabled = true;
            LocalUSMRadio.IsEnabled = false;
            ClearInfo();
            InputText.Text = string.Empty;
        }

        public void OnOpenListClicked(object sender, RoutedEventArgs e)
        {
            ListAnimation(AnimationType.On);
        }

        public void OnCheckClicked(object sender, RoutedEventArgs e)
        {
            CheckYoutubeLink();
        }

        public void OnInputSelectClicked(object sender, RoutedEventArgs e)
        {
            InputName = SelectInputFile();
            SetMediaInfo();
        }

        public void OnOutputSelectClicked(object sender, RoutedEventArgs e)
        {
            OutputName = SelectOutputFile();
            OutputText.Text = OutputName;
        }
        public void OnInputUSMSelectClicked(object sender, RoutedEventArgs e)
        {
            InputName = SelectUSMInputFile();
            InputText.Text = InputName;
        }

        public void CompatibilityCheck(object sender, RoutedEventArgs e)
        {
            if ((bool)VP9Radio.IsChecked)
            {
                TargetCodecType = CodecType.VP9;
                Suffix = ".ivf";
                OnConvertClicked(null, null);
            }
            else
            {
                TargetCodecType = CodecType.H264;
                Suffix = ".h264";
                ConfirmationDialog.Show();
            }
        }

        public void OnConvertClicked(object sender, RoutedEventArgs e)
        {
            ConfirmationDialog.Hide();
            if ((bool)VP9Radio.IsChecked)
            {
                TargetCodecType = CodecType.VP9;
                Suffix = ".ivf";
            }
            else
            {
                TargetCodecType = CodecType.H264;
                Suffix = ".h264";
            }
            Volume = (VolumeBox.Value / 100).ToString();
            Brightness = (BrightnessBox.Value / 100).ToString();
            CRF = CRFBox.Value.ToString();
            InputName = InputText.Text;
            OutputName = OutputText.Text;
            Key.Key = KeyBox.Text;
            try
            {
                if (!CurrentMedia.IsAudioAvailable)
                {
                    CurrentAudioState = ProgramAudioState.NoAudio;
                }
                if (CurrentWorkState == WorkState.Online)
                {
                    if (videoStreamInfo != null && OutputName != "" && Key.KeyVaild)
                    {
                        OutputPath = Path.GetDirectoryName(OutputName);
                        OutputFileName = Path.GetFileNameWithoutExtension(OutputName);
                        ProgressGrid.Visibility = Visibility.Visible;
                        UIState.Text = string.Empty;
                        UIState2.Text = string.Empty;
                        UIProgressBar.Value = 0;
                        VideoProgress = 0;
                        AudioProgress = 0;
                        PythonCompleted = false;
                        LockUI();
                        DownloadProcess();
                        UIProgressBar.Foreground = ThemeColor;
                    }
                    else
                    {
                        SnackBarManager(SnackBarType.InputOutputError);
                    }
                }
                else if (CurrentWorkState == WorkState.Local)
                {
                    if (InputName != "" && OutputName != "" && Key.KeyVaild)
                    {
                        OutputPath = Path.GetDirectoryName(OutputName);
                        OutputFileName = Path.GetFileNameWithoutExtension(OutputName);
                        ProgressGrid.Visibility = Visibility.Visible;
                        UIState.Text = string.Empty;
                        UIState2.Text = string.Empty;
                        UIProgressBar.Value = 0;
                        VideoProgress = 0;
                        AudioProgress = 0;
                        PythonCompleted = false;
                        LockUI();
                        LocalVideoConvertProcess();
                        UIProgressBar.Foreground = ThemeColor;
                    }
                    else
                    {
                        SnackBarManager(SnackBarType.InputOutputError);
                    }
                }
                else if (CurrentWorkState == WorkState.ExtractUSM)
                {
                    if (InputName != "" && OutputName != "" && Key.KeyVaild)
                    {
                        OutputPath = Path.GetDirectoryName(OutputName);
                        OutputFileName = Path.GetFileNameWithoutExtension(OutputName);
                        ProgressGrid.Visibility = Visibility.Visible;
                        UIState.Text = string.Empty;
                        UIState2.Text = string.Empty;
                        UIProgressBar.Value = 0;
                        VideoProgress = 0;
                        AudioProgress = 0;
                        PythonCompleted = false;
                        LockUI();
                        ExtractUSMPythonProcess();
                        UIProgressBar.Foreground = ThemeColor;
                    }
                    else
                    {
                        SnackBarManager(SnackBarType.InputOutputError);
                    }
                }

            }
            catch (Exception)
            {
                SnackBarManager(SnackBarType.ConvertError);
            }

        }

        public void OnDownloadProgressChanged(object sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            if (CurrentAudioState == ProgramAudioState.DownloadingAudio)
            {
                AudioDownloadProgress = e.ProgressPercentage;
                VideoDownloadProgress = 0;
            }
            else if (CurrentVideoState == ProgramVideoState.DownloadingVideo)
            {
                VideoDownloadProgress = e.ProgressPercentage;
                AudioDownloadProgress = 100;
            }
            UpdateProgressBar();
        }

        public async void DownloadProcess()
        {
            CurrentAudioState = ProgramAudioState.DownloadingAudio;
            UIState.Text = "DownloadingAudio";
            var aurl = audioStreamInfo.Url;
            var asave = OutputPath + "\\" + CurrentMedia.VideoSafeTitle + "audio." + "webm";
            Debug.WriteLine(aurl);
            Debug.WriteLine(asave);
            await downloader.DownloadFileTaskAsync(aurl, asave);
            CurrentAudioState = ProgramAudioState.DownloadedAudio;
            UIState.Text = "DownloadedAudio";
            CurrentVideoState = ProgramVideoState.DownloadingVideo;
            AudioFFmpegProcess(asave);
            UIState2.Text = "DownloadingVideo";
            var vurl = videoStreamInfo.Url;
            var vsave = OutputPath + "\\" + CurrentMedia.VideoSafeTitle + videoStreamInfo.VideoQuality.Label + "." + "webm";
            Debug.WriteLine(vurl);
            await downloader.DownloadFileTaskAsync(vurl, vsave);
            CurrentVideoState = ProgramVideoState.DownloadedVideo;
            UIState2.Text = "DownloadedVideo";
            VideoFFmpegProcess(vsave);
        }

        public void LocalVideoConvertProcess()
        {
            VideoFFmpegProcess();
            if (CurrentMedia.IsAudioAvailable)
            {
                CurrentAudioState = ProgramAudioState.NoAudio;
                UIState.Text = "AudioNotAvailable";
                AudioFFmpegProcess();
            }
            else
            {
                AudioProgress = 100;
            }
        }

        public void UpdateProgressBar(bool ForceSuccess = false)
        {
            if ((DateTime.Now - _LastUpdateTime) > TimeSpan.FromSeconds(0.25))
            {
                _LastUpdateTime = DateTime.Now;
                double progress = 0;
                var online = (CurrentWorkState == WorkState.Online ? true : false);
                string ProgressTimeText;
                if (online)
                {
                    progress = (AudioDownloadProgress * 0.1) + (VideoDownloadProgress * 0.35) + (AudioProgress * 0.05) + (VideoProgress * 0.45) + (PythonCompleted ? 5 : 0);

                }
                else
                {
                    progress = (VideoProgress * 0.75) + (AudioProgress * 0.2) + (PythonCompleted ? 5 : 0);
                }
                var UpdateUsedTime = DateTime.Now - LastUpdateTime;
                var ProgressDiff = progress - LastDownloadProgress;
                var ProgressSpeed = (ProgressDiff / UpdateUsedTime.Ticks) == 0 ? 0.0000000001 : (ProgressDiff / UpdateUsedTime.Ticks);
                var ProgressRemain = 100 - progress;
                var ProgressNeedTime = ProgressRemain / ProgressSpeed;
                ProgressTimeText = TimeSpan.FromTicks((long)ProgressNeedTime).ToString("mm\\:ss");
                if ((DateTime.Now - LastUpdateTime) > TimeSpan.FromSeconds(120))
                {
                    LastUpdateTime = DateTime.Now;
                    LastDownloadProgress = progress;
                }
                Dispatcher.InvokeAsync(() =>
                {
                    UIProgressBar.Value = progress;
                    UIProgressText.Text = progress.ToString("0.00") + "%";
                    UIProgressTime.Text = ProgressTimeText;
                    if (ForceSuccess)
                    {
                        UIProgressBar.Value = 100;
                    }
                    if (UIProgressBar.Value == 100)
                    {
                        LockUI(false);
                    }
                });
            }


        }
        public void RecommendCodec()
        {
            try
            {
                if (CurrentMedia.VideoCodecThreeShort == "vp9")
                {
                    VP9Text.Text = "VP9 (Recommended)";
                    H264Text.Text = "H.264";
                    VP9Radio.IsChecked = true;
                    H264Radio.IsChecked = false;
                }
                else if (CurrentMedia.VideoCodecThreeShort == "h26" || CurrentMedia.VideoCodecThreeShort == "avc")
                {
                    VP9Text.Text = "VP9";
                    H264Text.Text = "H.264 (Will Be Faster)";
                    VP9Radio.IsChecked = false;
                    H264Radio.IsChecked = true;
                }
                else
                {
                    VP9Text.Text = "VP9";
                    H264Text.Text = "H.264";
                    VP9Radio.IsChecked = true;
                    H264Radio.IsChecked = false;
                }
            }
            catch (Exception)
            {
                VP9Text.Text = "VP9";
                H264Text.Text = "H.264";
                VP9Radio.IsChecked = true;
                H264Radio.IsChecked = false;
            }

        }
        public string CopyAvailableCheck()
        {
            string Codec;
            string TargetVideoCodec;
            var VideoCodecCopy = CurrentMedia.VideoCodecThreeShort;
            Codec = (TargetCodecType == CodecType.VP9) ? "vp9" : "libx264";
            TargetVideoCodec = (TargetCodecType == CodecType.VP9) ? "vp9" : "h264";
            var TargetVideoCodecThreeShort = TargetVideoCodec.Substring(0, 3);
            if (VideoCodecCopy == "avc")
            {
                VideoCodecCopy = "h26";
            }
            if (TargetVideoCodecThreeShort == VideoCodecCopy && Brightness == "1")
            {
                Codec = "copy";
            }
            return Codec;
        }

        public void VideoFFmpegProcess(string input = null)
        {
            CurrentVideoState = ProgramVideoState.ConvertingVideo;
            UIState2.Text = "ConvertingVideo";
            Action<double> progressHandler = new Action<double>(p =>
            {
                if (p <= 100 && p >= 0)
                {
                    VideoProgress = p;
                }
                UpdateProgressBar();
                if (p == 100)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        UIState2.Text = "ConvertedVideo";
                    });
                    CreateUSMPythonProcess();
                }
            });
            if (LocalRadio.IsChecked == true)
            {
                input = InputName;
            }
            var test = CopyAvailableCheck();
            var bitrate = (int)CurrentMedia.VideoBitRate <= 6000000 ? 6000000 : (int)CurrentMedia.VideoBitRate;
            var crf = int.Parse(CRF);
            Debug.WriteLine((int)CurrentMedia.VideoBitRate <= 4096 ? 1000000 : (int)CurrentMedia.VideoBitRate);
            FFMpegArguments.FromFileInput(input)
                .OutputToFile(OutputPath + "\\" + SafeOutputName + Suffix, true, options => options
                .WithVideoCodec(CopyAvailableCheck())
                .WithCustomArgument(CopyAvailableCheck() == "copy" ? "-an" : "-an -b:v " + bitrate + " -crf " + crf + " -threads " + Environment.ProcessorCount + " -speed 4 -vf curves=all=\"0/0 1/" + Brightness + "\"")
                .WithFastStart()
                )
                .NotifyOnProgress(progressHandler, CurrentMedia.VideoDuration)
            .ProcessAsynchronously();
        }

        public void AudioFFmpegProcess(string input = null)
        {
            CurrentAudioState = ProgramAudioState.ConvertingAudio;
            UIState.Text = "ConvertingAudio";
            Action<double> progressHandler = new Action<double>(p =>
            {
                AudioProgress = p;
                UpdateProgressBar();
                if (p == 100)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        UIState.Text = "ConvertedAudio";
                    });
                }
            });
            if (LocalRadio.IsChecked == true)
            {
                input = InputName;
            }
            FFMpegArguments.FromFileInput(input)
                .OutputToFile(OutputPath + "\\" + OutputFileName + ".ogg", true, options => options
                .WithAudioCodec(AudioCodec.LibVorbis)
                .WithAudioSamplingRate(44100)
                .WithAudioBitrate(320)
                .WithCustomArgument("-vn" + ((Volume == "1") ? "" : " -af volume=" + Volume))
                )
                .NotifyOnProgress(progressHandler, CurrentMedia.VideoDuration)
            .ProcessAsynchronously();
        }

        public void CreateUSMPythonProcess()
        {
            CurrentVideoState = ProgramVideoState.WaitforWannaCri;
            Dispatcher.InvokeAsync(() =>
            {
                UIState2.Text = "WaitingforWannaCri";
            });
            Thread.Sleep(1000);//WaitFFMpegWrite
            Process process = new Process();
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory + "\\FFmpeg";
            process.StartInfo.FileName = Environment.CurrentDirectory + "\\Python\\python.exe";
            var key = string.Empty;
            Dispatcher.Invoke(() =>
            {
                key = KeyBox.Text;
            });
            if (Key.UseKey)
            {
                process.StartInfo.Arguments = " -m wannacri createusm \"" + OutputPath + "\\" + SafeOutputName + Suffix + "\" --output \"" + OutputPath + "\\" + SafeOutputName + "\"" + " --key " + key;

            }
            else
            {
                process.StartInfo.Arguments = " -m wannacri createusm \"" + OutputPath + "\\" + SafeOutputName + Suffix + "\" --output \"" + OutputPath + "\\" + SafeOutputName + "\"";
            }
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();
            process.Close();
            CurrentVideoState = ProgramVideoState.RenameFile;
            Dispatcher.InvokeAsync(() =>
            {
                UIState2.Text = "MovingFile";
            });
            if (File.Exists(OutputPath + "\\" + SafeOutputName + ".usm"))
            {
                try
                {
                    if (File.Exists(OutputPath + "\\" + OutputFileName + ".usm"))
                    {
                        File.Delete(OutputPath + "\\" + OutputFileName + ".usm");
                    }
                    if (!(SafeOutputName == OutputFileName))
                    {
                        File.Move(OutputPath + "\\" + SafeOutputName + ".usm", OutputPath + "\\" + OutputFileName + ".usm");

                        if (File.Exists(OutputPath + "\\" + SafeOutputName + Suffix))
                        {
                            File.Delete(OutputPath + "\\" + SafeOutputName + Suffix);
                        }
                    }
                    Dispatcher.Invoke(() =>
                    {
                        SnackBarManager(SnackBarType.Success);
                        UIProgressBar.Foreground = SuccessColor;
                        UIState.Text = string.Empty;
                        UIState2.Text = string.Empty;
                    });
                }
                catch (Exception)
                {
                    Dispatcher.Invoke(() =>
                    {
                        SnackBarManager(SnackBarType.ConvertError);
                        UIProgressBar.Foreground = ErrorColor;
                    });
                }

                CurrentVideoState = ProgramVideoState.None;

            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    SnackBarManager(SnackBarType.ConvertError);
                    UIProgressBar.Foreground = ErrorColor;
                });
            }
            PythonCompleted = true;
            UpdateProgressBar();
        }

        public void ExtractUSMPythonProcess()
        {
            Process process = new Process();
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory + "\\FFmpeg";
            process.StartInfo.FileName = Environment.CurrentDirectory + "\\Python\\python.exe";
            if (Key.UseKey)
            {
                process.StartInfo.Arguments = " -m wannacri extractusm \"" + InputName + "\" --output \"" + OutputPath + "\"" + " --key " + Key.Key;
            }
            else
            {
                process.StartInfo.Arguments = " -m wannacri extractusm \"" + InputName + "\" --output \"" + OutputPath + "\"";
            }
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();
            process.Close();
            //if (File.Exists(OutputPath + "\\" + SafeOutputName + ".usm"))
            //{
            //    if (File.Exists(OutputPath + "\\" + OutputFileName + ".usm"))
            //    {
            //        File.Delete(OutputPath + "\\" + OutputFileName + ".usm");
            //    }
            //    if (File.Exists(OutputPath + "\\" + SafeOutputName + Suffix))
            //    {
            //        File.Delete(OutputPath + "\\" + SafeOutputName + Suffix);
            //    }
            //    File.Move(OutputPath + "\\" + SafeOutputName + ".usm", OutputPath + "\\" + OutputFileName + ".usm");
            //    Dispatcher.Invoke(() =>
            //    {
            //        SnackBarManager(SnackBarType.Success);
            //        UIProgressBar.Foreground = SuccessColor;
            //        UIState.Text = string.Empty;
            //        UIState2.Text = string.Empty;
            //    });
            //    CurrentVideoState = ProgramVideoState.None;

            //}
            //else
            //{
            //    Dispatcher.Invoke(() =>
            //    {
            //        SnackBarManager(SnackBarType.ConvertError);
            //        UIProgressBar.Foreground = ErrorColor;
            //    });
            //}
            UpdateProgressBar(true);
            Dispatcher.Invoke(() =>
            {
                SnackBarManager(SnackBarType.Success);
                UIProgressBar.Foreground = SuccessColor;
                UIState.Text = string.Empty;
                UIState2.Text = string.Empty;
            });
        }
    }
}
