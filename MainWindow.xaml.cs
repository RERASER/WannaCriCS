using System.Diagnostics;
using System.Windows;
using System;
using FFMpegCore;
using FFMpegCore.Enums;
using System.ComponentModel;
using System.IO;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Interfaces;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Linq;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Threading;
using System.Net;
using Downloader;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Net.Http;

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

        public string SafeOutputName = "Output";

        public string OutputPath;

        public string OutputFileName;

        public string MediaInfo;

        public string VideoTitle;

        public string VideoSafeTitle;

        public StreamManifest streamManifest;

        public IVideoStreamInfo videoStreamInfo;

        public IStreamInfo audioStreamInfo;

        public string CurrentVideoCodec;

        public string CurrentVideoCodecThreeShort;

        public string CurrentAudioCodec;

        public TimeSpan VideoDuration;

        public CodecType _VideoCodec;

        public string CRF;

        public string Volume;

        public string Brightness;

        public double VideoProgress;

        public double VideoDownloadProgress;

        public double AudioProgress;

        public double AudioDownloadProgress;

        public bool PythonCompleted;
        //AudioDownloadProgress 10% VideoDownloadProgress 35% AudioProgress 5% VideoProgress 45% Python 3% Rename 2%
        //AudioProgress 10% VideoProgress 80% Python 6% Rename 4%

        public bool IsAudioAvailable;

        public bool ProcessLock;

        public object SelectedItem
        {
            get; set;
        }

        public static readonly DownloadConfiguration downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 8, // file parts to download, default value is 1
            MaxTryAgainOnFailover = 10,
            ParallelDownload = true, // download parts of file as parallel or not. Default value is false
            RequestConfiguration =
                {
                    Proxy = WebProxy.GetDefaultProxy(),
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36"
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

        public static readonly DoubleAnimation CodecListOut = new DoubleAnimation();

        public static readonly DoubleAnimation CodecListIn = new DoubleAnimation();

        public MainWindow()
        {
            InitializeComponent();

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

            CodecListOut.To = 65;
            CodecListOut.Duration = TimeSpan.FromSeconds(0.5);
            CodecListOut.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 1
            };

            CodecListIn.To = 0;
            CodecListIn.Duration = TimeSpan.FromSeconds(0.5);
            CodecListIn.EasingFunction = new BackEase()
            {
                EasingMode = EasingMode.EaseIn,
                Amplitude = 1
            };

            downloader.DownloadProgressChanged += OnDownloadProgressChanged;

            InitFFmpeg();
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

        public void InitFFmpeg()
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = Environment.CurrentDirectory + "\\FFmpeg");
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
                    UIVideoList.IsEnabled = false;
                    break;
            }
        }

        public void LockUI(bool isLocked = true)
        {
            if (isLocked)
            {
                InputSelect.IsEnabled = false; OutputSelect.IsEnabled = false;
                UrlText.IsEnabled = false; UrlSelect.IsEnabled = false;
                LocalRadio.IsEnabled = false; OnlineRadio.IsEnabled = false;
                VP9Radio.IsEnabled = false; H264Radio.IsEnabled = false;
                CRFBox.IsEnabled = false; VolumeBox.IsEnabled = false; BrightnessBox.IsEnabled = false;
                UrlText.IsEnabled = false;
                ConvertButton.IsEnabled = false;
                ListAnimation(AnimationType.Off);
                ProcessLock = true;
                
            }
            else
            {
                InputSelect.IsEnabled = true; OutputSelect.IsEnabled = true;
                UrlText.IsEnabled = false; UrlSelect.IsEnabled = false;
                VP9Radio.IsEnabled = true; H264Radio.IsEnabled = true;
                CRFBox.IsEnabled = true; VolumeBox.IsEnabled = true; BrightnessBox.IsEnabled = true;
                UrlText.IsEnabled = true;
                ConvertButton.IsEnabled = true;
                ProcessLock = false;
                if (OnlineRadio.IsChecked == true)
                {
                    LocalRadio.IsEnabled = true;
                }
                else
                {
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
                    ErrorBar.Content = "Input or Output is empty.";
                    ErrorBar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
                    ErrorBar.Icon = Wpf.Ui.Common.SymbolRegular.ErrorCircle24;
                    ErrorBar.Timeout = 2000;
                    ErrorBar.Show();
                    break;
                case SnackBarType.LinkError:
                    ErrorBar.Content = "Not a valid link or network error.";
                    ErrorBar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
                    ErrorBar.Icon = Wpf.Ui.Common.SymbolRegular.ErrorCircle24;
                    ErrorBar.Timeout = 6000;
                    ErrorBar.Show();
                    break;
                case SnackBarType.NotValid:
                    ErrorBar.Content = "Not a valid video file.";
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
                VideoTitle = video.Title;
                VideoSafeTitle = System.Text.Encodings.Web.UrlEncoder.Default.Encode(VideoTitle);
                var maxLength = 23;
                var ShortVideoTitle = VideoTitle.Length > maxLength ? VideoTitle.Substring(0, maxLength - 13) + "...(Hover to see more)" : VideoTitle;
                VideoDuration = (TimeSpan)video.Duration;
                info.Text = "Title:" + ShortVideoTitle + " Duration:" + VideoDuration.ToString("hh\\:mm\\:ss");
                info.ToolTip = VideoTitle;
                info2.Text = "Getting Stream Info...";
                streamManifest = await Youtube.Videos.Streams.GetManifestAsync(VideoUrl);
                var VideoList = streamManifest.GetVideoOnlyStreams().Where(s => s.VideoCodec.Substring(0, 3) == "vp9" || s.VideoCodec.Substring(0, 3) == "avc").Where(s => s.VideoResolution.Height >= 480).ToList();
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
                CurrentVideoCodec = "";
                RecommendCodec();
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
                CurrentVideoCodec = videoStreamInfo.VideoCodec;
                CurrentVideoCodecThreeShort = CurrentVideoCodec.Substring(0, 3);
                RecommendCodec();
                info2.Text = "Video:" + VideoResolution + " " + CurrentVideoCodec + " Audio:Getting...";
                ListAnimation(AnimationType.Half);
                try
                {
                    audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    CurrentAudioCodec = audioStreamInfo.Bitrate.KiloBitsPerSecond.ToString("0");
                    info2.Text = "Video:" + VideoResolution + " " + VideoFrameRate + "fps " + CurrentVideoCodec + " Audio:" + CurrentAudioCodec + "Kbit/s";
                    IsAudioAvailable = true;
                }
                catch (Exception)
                {
                    IsAudioAvailable = false;
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
                CurrentVideoCodec = mediaInfo.PrimaryVideoStream.CodecName;
                CurrentVideoCodecThreeShort = CurrentVideoCodec.Substring(0, 3);
                VideoDuration = mediaInfo.PrimaryVideoStream.Duration;
                InputText.Text = InputName;
                try
                {
                    if (mediaInfo.PrimaryAudioStream.CodecName != null)
                    {
                        CurrentAudioCodec = mediaInfo.PrimaryAudioStream.CodecName;
                    }
                    if (CurrentAudioCodec != null && CurrentAudioCodec != "" && CurrentAudioCodec != "NotAvailable")
                    {
                        IsAudioAvailable = true;
                    }
                }
                catch (Exception)
                {
                    CurrentAudioCodec = "NotAvailable";
                    IsAudioAvailable = false;
                }
                RecommendCodec();
                info.Text = "Video:" + CurrentVideoCodec + " Audio:" + CurrentAudioCodec + " Duration:" + VideoDuration.ToString("hh\\:mm\\:ss");
            }
            catch (Exception)
            {
                InputName = InputText.Text;
                SnackBarManager(SnackBarType.NotValid);
            }
        }

        public void RecommendCodec()
        {
            if (CurrentVideoCodecThreeShort == "vp9")
            {
                VP9Text.Text = "VP9 (Recommended)";
                H264Text.Text = "H.264";
                VP9Radio.IsChecked = true;
                H264Radio.IsChecked = false;
            }
            else if (CurrentVideoCodecThreeShort == "h26" || CurrentVideoCodecThreeShort == "avc")
            {
                VP9Text.Text = "VP9";
                H264Text.Text = "H.264 (Recommended)";
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

        public string SelectOutputFile()
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog { Filter = "*.usm|*.usm" };

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
            ListAnimation(AnimationType.Off);
            VideoTitle = null;
            VideoSafeTitle = null;
            streamManifest = null;
            videoStreamInfo = null;
            audioStreamInfo = null;
            MediaInfo = null;
            CurrentVideoCodec = null;
            CurrentVideoCodecThreeShort = null;
            CurrentAudioCodec = null;
            IsAudioAvailable = false;
            VideoDownloadProgress = 0;
            AudioDownloadProgress = 0;
            VideoProgress = 0;
            AudioProgress = 0;
            info.Text = "Waiting...";
            info2.Text = string.Empty;
            UIState.Text = string.Empty;
            UIState2.Text = string.Empty;
            UIVideoList.Items.Clear();
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
            if (true)
            {

            }
        }

        public void OnCancelClicked(object sender, EventArgs e)
        {
            ExitDialog.Hide();
        }

        public void OnOnlineRadioClicked(object sender, RoutedEventArgs e)
        {
            OnlineInput.BeginAnimation(MarginProperty, SlideIn);
            OnlineInput.BeginAnimation(OpacityProperty, FadeIn);
            LocalInput.BeginAnimation(MarginProperty, SlideOut);
            LocalInput.BeginAnimation(OpacityProperty, FadeOut);
            Codec.BeginAnimation(HeightProperty, CodecListIn);
            UrlText.IsEnabled = true;
            UrlSelect.IsEnabled = true;
            InputSelect.IsEnabled = false;
            OnlineRadio.IsEnabled = false;
            LocalRadio.IsEnabled = true;
            ClearInfo();
        }

        public void OnLocalRadioClicked(object sender, RoutedEventArgs e)
        {
            OnlineInput.BeginAnimation(MarginProperty, SlideOut);
            OnlineInput.BeginAnimation(OpacityProperty, FadeOut);
            LocalInput.BeginAnimation(MarginProperty, SlideIn);
            LocalInput.BeginAnimation(OpacityProperty, FadeIn);
            Codec.BeginAnimation(HeightProperty, CodecListOut);
            UrlText.IsEnabled = false;
            UrlSelect.IsEnabled = false;
            InputSelect.IsEnabled = true;
            OnlineRadio.IsEnabled = true;
            LocalRadio.IsEnabled = false;
            ClearInfo();
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
            //Task.Run(() => { FFProbeProcess(); });
            SetMediaInfo();
        }

        public void OnOutputSelectClicked(object sender, RoutedEventArgs e)
        {
            OutputName = SelectOutputFile();
            //Task.Run(() => { FFProbeProcess(); });
            OutputText.Text = OutputName;
        }

        public void OnConvertClicked(object sender, RoutedEventArgs e)
        {
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
            if (!IsAudioAvailable)
            {
                CurrentAudioState = ProgramAudioState.NoAudio;
            }
            try
            {
                if (OnlineRadio.IsChecked == true)
                {
                    if (videoStreamInfo != null && OutputName != "")
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
                else
                {
                    if (InputName != "" && OutputName != "")
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
                        ConvertProcess();
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
            var asave = OutputPath + "\\" + VideoSafeTitle + "audio." + "webm";
            //Debugger.Break();
            Debug.WriteLine(aurl);
            Debug.WriteLine(asave);
            await downloader.DownloadFileTaskAsync(aurl, asave);
            CurrentAudioState = ProgramAudioState.DownloadedAudio;
            UIState.Text = "DownloadedAudio";
            CurrentVideoState = ProgramVideoState.DownloadingVideo;
            AudioFFmpegProcess(asave);
            UIState2.Text = "DownloadingVideo";
            var vurl = videoStreamInfo.Url;
            var vsave = OutputPath + "\\" + VideoSafeTitle + "." + "webm";
            //Debugger.Break();
            Debug.WriteLine(vurl);
            await downloader.DownloadFileTaskAsync(vurl, vsave);
            CurrentVideoState = ProgramVideoState.DownloadedVideo;
            UIState2.Text = "DownloadedVideo";
            VideoFFmpegProcess(vsave);
        }

        public void ConvertProcess()
        {
            VideoFFmpegProcess();
            if (IsAudioAvailable)
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

        public void UpdateProgressBar()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (OnlineRadio.IsChecked == true)
                {
                    UIProgressBar.Value = (AudioDownloadProgress * 0.1) + (VideoDownloadProgress * 0.35) + (AudioProgress * 0.05) + (VideoProgress * 0.45) + (PythonCompleted ? 5 : 0);
                }
                else
                {
                    UIProgressBar.Value = (VideoProgress * 0.75) + (AudioProgress * 0.2) + (PythonCompleted ? 5 : 0);
                }
                UIProgressText.Text = UIProgressBar.Value.ToString("0.00") + "%";
            });
            Dispatcher.InvokeAsync(() =>
            {
                if (UIProgressBar.Value == 100)
                {
                    LockUI(false);
                }
            });
        }

        public string CopyAvailableCheck()
        {
            string Codec;
            string TargetVideoCodec;
            Codec = (TargetCodecType == CodecType.VP9) ? "vp9" : "libx264";
            TargetVideoCodec = (TargetCodecType == CodecType.VP9) ? "vp9" : "h264";
            var TargetVideoCodecThreeShort = TargetVideoCodec.Substring(0, 3);
            if (CurrentVideoCodecThreeShort == "avc")
            {
                CurrentVideoCodecThreeShort = "h26";
            }
            if (TargetVideoCodecThreeShort == CurrentVideoCodecThreeShort && Brightness == "1")
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
                VideoProgress = p;
                UpdateProgressBar();
                if (p == 100)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        UIState2.Text = "ConvertedVideo";
                    });
                    PythonProcess();
                }
            });
            if (LocalRadio.IsChecked == true)
            {
                input = InputName;
            }
            var test = CopyAvailableCheck();
            FFMpegArguments.FromFileInput(input)
                .OutputToFile(OutputPath + "\\" + SafeOutputName + Suffix, true, options => options
                .WithVideoCodec(CopyAvailableCheck())
                .WithConstantRateFactor(int.Parse(CRF))
                .WithCustomArgument(CopyAvailableCheck() == "copy" ? "-an" : "-an -vf curves=all=\"0/0 1/" + Brightness + "\"")
                .WithFastStart()
                )
                .NotifyOnProgress(progressHandler, VideoDuration)
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
                .WithCustomArgument("-vn" + ((Volume=="1")?"":" -af volume="+Volume))
                )
                .NotifyOnProgress(progressHandler, VideoDuration)
            .ProcessAsynchronously();
        }

        public void PythonProcess()
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
            process.StartInfo.Arguments = " -m wannacri createusm \"" + OutputPath + "\\" + SafeOutputName + Suffix + "\" --output \"" + OutputPath + "\\" + SafeOutputName + "\"";
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
                if (File.Exists(OutputPath + "\\" + OutputFileName + ".usm"))
                {
                    File.Delete(OutputPath + "\\" + OutputFileName + ".usm");
                }
                if (File.Exists(OutputPath + "\\" + SafeOutputName + Suffix))
                {
                    File.Delete(OutputPath + "\\" + SafeOutputName + Suffix);
                }
                File.Move(OutputPath + "\\" + SafeOutputName + ".usm", OutputPath + "\\" + OutputFileName + ".usm");
                Dispatcher.Invoke(() =>
                {
                    SnackBarManager(SnackBarType.Success);
                    UIProgressBar.Foreground = SuccessColor;
                    UIState.Text = string.Empty;
                    UIState2.Text = string.Empty;
                });
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
    }
}
