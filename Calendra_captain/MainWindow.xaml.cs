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
        private Forms.NotifyIcon? _notifyIcon;

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

        private readonly string[] _gregorianMonthNamesFa =
        {
            "ژانویه",
            "فوریه",
            "مارس",
            "آوریل",
            "مه",
            "ژوئن",
            "ژوئیه",
            "آگوست",
            "سپتامبر",
            "اکتبر",
            "نوامبر",
            "دسامبر"
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

            InitializeTrayIcon();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDates();

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
                UpdateLayoutForCurrentMode();
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

            UpdateGregorianMonthsHelp(now);
        }

        private void UpdateGregorianMonthsHelp(DateTime now)
        {
            string[] monthLines = new string[12];

            for (int month = 1; month <= 12; month++)
            {
                int daysInMonth = DateTime.DaysInMonth(now.Year, month);
                string days = ToPersianDigits(daysInMonth.ToString(CultureInfo.InvariantCulture));

                monthLines[month - 1] =
                    $"{month:00} - {_gregorianMonthNames[month - 1]} / " +
                    $"{_gregorianMonthNamesFa[month - 1]} — {days} روز";
            }

            GregorianMonthsTitleText.Text =
                $"تعداد روزهای ماه‌های میلادی {ToPersianDigits(now.Year.ToString(CultureInfo.InvariantCulture))}";
            GregorianMonthsHelpText.Text = string.Join(Environment.NewLine, monthLines);

            DateTime startOfNextMonth =
                new DateTime(now.Year, now.Month, 1).AddMonths(1);
            TimeSpan remaining = startOfNextMonth - now;

            string remainingDays =
                ToPersianDigits(((int)remaining.TotalDays).ToString(CultureInfo.InvariantCulture));
            string remainingHours =
                ToPersianDigits(remaining.Hours.ToString(CultureInfo.InvariantCulture));
            string remainingMinutes =
                ToPersianDigits(remaining.Minutes.ToString(CultureInfo.InvariantCulture));

            MonthCountdownText.Text =
                $"{_gregorianMonthNamesFa[now.Month - 1]}: " +
                $"{remainingDays} روز و {remainingHours} ساعت و {remainingMinutes} دقیقه";
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
                CollapseAtCurrentPosition();
            else
                ExpandAtCurrentPosition();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "Calendra",
                Visible = true,
                ContextMenuStrip = new Forms.ContextMenuStrip()
            };

            _notifyIcon.ContextMenuStrip.Items.Add("نمایش برنامه", null, (_, _) => ShowFromTray());
            _notifyIcon.ContextMenuStrip.Items.Add("مخفی کردن", null, (_, _) => HideToTray());
            _notifyIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("بستن Calendra", null, (_, _) => Dispatcher.Invoke(() => SW.Application.Current.Shutdown()));
            _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);

            Closed += (_, _) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            };
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

        private void ExpandAtCurrentPosition()
        {
            double right = Left + Width;

            Width = ExpandedWidth;
            Height = ExpandedHeight;

            DatePanel.Visibility = Visibility.Visible;
            SmallLabelText.Visibility = Visibility.Visible;

            Left = right - Width;

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

        private void CollapseAtCurrentPosition()
        {
            double right = Left + Width;

            Width = CollapsedWidth;
            Height = CollapsedHeight;

            DatePanel.Visibility = Visibility.Collapsed;
            SmallLabelText.Visibility = Visibility.Collapsed;

            Left = right - Width;

            _isExpanded = false;

            ArrowText.Text = "‹";
            ToggleHandle.CornerRadius = new CornerRadius(18);
            MainBox.CornerRadius = new CornerRadius(18);
        }

        private void UpdateLayoutForCurrentMode()
        {
            if (_isExpanded)
            {
                Width = ExpandedWidth;
                Height = ExpandedHeight;
                DatePanel.Visibility = Visibility.Visible;
                SmallLabelText.Visibility = Visibility.Visible;
                ArrowText.Text = "›";
                ToggleHandle.CornerRadius = new CornerRadius(0, 18, 18, 0);
            }
            else
            {
                Width = CollapsedWidth;
                Height = CollapsedHeight;
                DatePanel.Visibility = Visibility.Collapsed;
                SmallLabelText.Visibility = Visibility.Collapsed;
                ArrowText.Text = "‹";
                ToggleHandle.CornerRadius = new CornerRadius(18);
            }

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
            HideToTray();
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                HideToTray();
                return;
            }

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
                        ExpandAtCurrentPosition();
                    else
                        CollapseAtCurrentPosition();

                    _isRestoringFromMinimize = false;
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        private void HideToTray()
        {
            _stateBeforeMinimize = _isExpanded;
            Hide();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Topmost = true;
            Activate();

            if (_stateBeforeMinimize)
                ExpandAtCurrentPosition();
            else
                CollapseAtCurrentPosition();
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
