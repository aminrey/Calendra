using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;
using SWM = System.Windows.Media;
using SW = System.Windows;

namespace Calendra
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly PersianCalendar _persianCalendar = new PersianCalendar();

        private const double ExpandedWidth = 350;
        private const double ExpandedHeight = 104;

        private const double CollapsedWidth = 42;
        private const double CollapsedHeight = 52;

        private const double RightMargin = 8;
        private const double BottomMargin = 12;

        private const string AppRegistryPath = @"Software\Calendra";
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupQuestionValueName = "StartupQuestionAsked";
        private const string RunValueName = "Calendra";

        private bool _isExpanded = false;
        private bool _stateBeforeMinimize = false;
        private bool _isRestoringFromMinimize = false;

        private readonly string[] _persianMonthNames =
        {
            "فروردین",
            "اردیبهشت",
            "خرداد",
            "تیر",
            "مرداد",
            "شهریور",
            "مهر",
            "آبان",
            "آذر",
            "دی",
            "بهمن",
            "اسفند"
        };

        private readonly string[] _gregorianMonthNames =
        {
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December"
        };

        public MainWindow()
        {
            InitializeComponent();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };

            _timer.Tick += Timer_Tick;

            // همیشه در حالت باز شده اجرا شود
            _isExpanded = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDates();
            UpdateGregorianMonthsHelp();

            ExpandFromRightSide();

            _timer.Start();

            Dispatcher.BeginInvoke(
                new Action(AskStartupQuestionFirstTime),
                DispatcherPriority.ApplicationIdle);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateDates();

            if (WindowState != WindowState.Minimized)
            {
                if (_isExpanded)
                    ExpandFromRightSide();
                else
                    CollapseToRightSide();
            }
        }

        private void UpdateDates()
        {
            DateTime now = DateTime.Now;

            int persianYear = _persianCalendar.GetYear(now);
            int persianMonth = _persianCalendar.GetMonth(now);
            int persianDay = _persianCalendar.GetDayOfMonth(now);

            string persianDate = $"{persianYear:0000}/{persianMonth:00}/{persianDay:00}";
            PersianDateText.Text = "شمسی: " + ToPersianDigits(persianDate);

            string gregorianDayName =
                now.ToString("dddd", CultureInfo.InvariantCulture);

            int gregorianYear = now.Year;
            int gregorianMonth = now.Month;
            int gregorianDay = now.Day;

            string gregorianMonthName = _gregorianMonthNames[gregorianMonth - 1];

            // تاریخ میلادی به صورت عددی
            string gregorianDateNumeric = $"{gregorianYear:0000}/{gregorianMonth:00}/{gregorianDay:00}";
            GregorianDateText.Text = "میلادی: " + gregorianDateNumeric;

            // نام روز میلادی به فارسی
            string gregorianDayNameFa = GetPersianDayName(now.DayOfWeek);
            // نام ماه و نام روز میلادی (انگلیسی) و فارسی
            string gregorianMonthDay = $"{gregorianMonthName} {gregorianDayName} / {gregorianDayNameFa}";
            GregorianMonthDayText.Text = gregorianMonthDay;
        }

        private void UpdateGregorianMonthsHelp()
        {
            string text =
                "01 - January / ژانویه\n" +
                "02 - February / فوریه\n" +
                "03 - March / مارس\n" +
                "04 - April / آوریل\n" +
                "05 - May / می\n" +
                "06 - June / ژوئن\n" +
                "07 - July / ژوئیه\n" +
                "08 - August / آگوست\n" +
                "09 - September / سپتامبر\n" +
                "10 - October / اکتبر\n" +
                "11 - November / نوامبر\n" +
                "12 - December / دسامبر";

            GregorianMonthsHelpText.Text = text;
        }

        private static string GetPersianDayName(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Saturday => "شنبه",
                DayOfWeek.Sunday => "یکشنبه",
                DayOfWeek.Monday => "دوشنبه",
                DayOfWeek.Tuesday => "سه‌شنبه",
                DayOfWeek.Wednesday => "چهارشنبه",
                DayOfWeek.Thursday => "پنجشنبه",
                DayOfWeek.Friday => "جمعه",
                _ => string.Empty
            };
        }

        private static string ToPersianDigits(string input)
        {
            string[] persianDigits =
            {
                "۰", "۱", "۲", "۳", "۴",
                "۵", "۶", "۷", "۸", "۹"
            };

            for (int i = 0; i < 10; i++)
            {
                input = input.Replace(i.ToString(), persianDigits[i]);
            }

            return input;
        }

        private void ToggleHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToggleVisibility();
        }

        private void ToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            ToggleVisibility();
        }

        private void ToggleVisibility()
        {
            if (_isExpanded)
                CollapseToRightSide();
            else
                ExpandFromRightSide();
        }

        private void ExpandFromRightSide()
        {
            Rect workArea = GetPrimaryScreenWorkAreaInWpfUnits();

            Width = ExpandedWidth;
            Height = ExpandedHeight;

            DatePanel.Visibility = Visibility.Visible;
            SmallLabelText.Visibility = Visibility.Visible;

            Left = workArea.Right - Width - RightMargin;
            Top = workArea.Bottom - Height - BottomMargin;

            _isExpanded = true;

            ArrowText.Text = "›";
            ToggleHandle.CornerRadius = new CornerRadius(0, 18, 18, 0);
            MainBox.CornerRadius = new CornerRadius(18);
        }

        private void CollapseToRightSide()
        {
            Rect workArea = GetPrimaryScreenWorkAreaInWpfUnits();

            Width = CollapsedWidth;
            Height = CollapsedHeight;

            DatePanel.Visibility = Visibility.Collapsed;
            SmallLabelText.Visibility = Visibility.Collapsed;

            Left = workArea.Right - Width - RightMargin;
            Top = workArea.Bottom - Height - BottomMargin;

            _isExpanded = false;

            ArrowText.Text = "‹";
            ToggleHandle.CornerRadius = new CornerRadius(18);
            MainBox.CornerRadius = new CornerRadius(18);
        }

        private Rect GetPrimaryScreenWorkAreaInWpfUnits()
        {
            Forms.Screen primaryScreen =
                Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];

            var workingArea = primaryScreen.WorkingArea;

            PresentationSource? source = PresentationSource.FromVisual(this);

            if (source?.CompositionTarget == null)
            {
                return new Rect(
                    workingArea.Left,
                    workingArea.Top,
                    workingArea.Width,
                    workingArea.Height);
            }

            SWM.Matrix transform = source.CompositionTarget.TransformFromDevice;

            SW.Point topLeft = transform.Transform(new SW.Point(workingArea.Left, workingArea.Top));
            SW.Point bottomRight = transform.Transform(new SW.Point(workingArea.Right, workingArea.Bottom));

            return new Rect(topLeft, bottomRight);
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MonthsPopup.IsOpen = !MonthsPopup.IsOpen;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            _stateBeforeMinimize = _isExpanded;
            WindowState = WindowState.Minimized;
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                if (_isRestoringFromMinimize)
                    return;

                _isRestoringFromMinimize = true;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Topmost = true;
                    Activate();

                    if (_stateBeforeMinimize)
                        ExpandFromRightSide();
                    else
                        CollapseToRightSide();

                    _isRestoringFromMinimize = false;
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SW.Application.Current.Shutdown();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            SW.Application.Current.Shutdown();
        }

        private void RefreshPosition_Click(object sender, RoutedEventArgs e)
        {
            if (_isExpanded)
                ExpandFromRightSide();
            else
                CollapseToRightSide();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void AskStartupQuestionFirstTime()
        {
            try
            {
                using RegistryKey? appKey =
                    Registry.CurrentUser.CreateSubKey(AppRegistryPath);

                object? askedValue =
                    appKey?.GetValue(StartupQuestionValueName);

                if (askedValue?.ToString() == "1")
                    return;

                MessageBoxResult result = WpfMessageBox.Show(
                    "آیا می‌خواهید Calendra همیشه با شروع ویندوز اجرا شود؟",
                    "Calendra Startup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (result == MessageBoxResult.Yes)
                {
                    EnableStartup();
                }

                appKey?.SetValue(
                    StartupQuestionValueName,
                    "1",
                    RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    "خطا در ثبت تنظیمات اجرای خودکار:\n" + ex.Message,
                    "Calendra",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void EnableStartup_Click(object sender, RoutedEventArgs e)
        {
            EnableStartup();

            WpfMessageBox.Show(
                "اجرای خودکار Calendra با شروع ویندوز فعال شد.",
                "Calendra",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DisableStartup_Click(object sender, RoutedEventArgs e)
        {
            DisableStartup();

            WpfMessageBox.Show(
                "اجرای خودکار Calendra حذف شد.",
                "Calendra",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // دکمه افزودن به استارتاپ
        private void AddToStartupButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = WpfMessageBox.Show(
                "آیا می‌خواهید برنامه به استارتاپ ویندوز اضافه شود؟",
                "افزودن به استارتاپ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                EnableStartup();

                WpfMessageBox.Show(
                    "برنامه با موفقیت به استارتاپ ویندوز اضافه شد.",
                    "استارتاپ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private static void EnableStartup()
        {
            string? exePath = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(exePath))
                return;

            using RegistryKey? runKey =
                Registry.CurrentUser.CreateSubKey(RunRegistryPath);

            runKey?.SetValue(
                RunValueName,
                $"\"{exePath}\"",
                RegistryValueKind.String);
        }

        private static void DisableStartup()
        {
            using RegistryKey? runKey =
                Registry.CurrentUser.CreateSubKey(RunRegistryPath);

            runKey?.DeleteValue(RunValueName, false);
        }

        private void CalendarIconButton_Click(object sender, RoutedEventArgs e)
        {
            OpenCalendarLinks();
        }

        private void CalendarTitle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://resna24.ir",
                UseShellExecute = true
            });
        }

        private static void OpenCalendarLinks()
        {
            string query = Uri.EscapeDataString("رسنا پشتیبانی شبکه کامپیوتری در کرج و تهران");

            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://www.google.com/search?q={query}",
                UseShellExecute = true
            });

            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.time.ir",
                UseShellExecute = true
            });

            Process.Start(new ProcessStartInfo
            {
                FileName = "https://resna24.ir",
                UseShellExecute = true
            });
        }

        private void DateText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.time.ir",
                UseShellExecute = true
            });
        }
    }
}