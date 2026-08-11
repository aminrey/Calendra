using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Forms = System.Windows.Forms;
using RadioButton = System.Windows.Controls.RadioButton;
using WpfMessageBox = System.Windows.MessageBox;
using SWM = System.Windows.Media;
using SW = System.Windows;

namespace Calendra
{
    public partial class MainWindow : Window
    {
        private enum MinimizeMode
        {
            Random,
            HeartbrokenHero,
            Bollywood,
            SpaceHero,
            DigitalPet,
            OfficeWorker
        }

        private sealed record MinimizeTheme(
            MinimizeMode Mode,
            string Title,
            string Icon,
            string Dialogue,
            string FinalMessage,
            string AccentColor,
            string SecondaryColor,
            string DismissButtonText,
            double HeroRotation);

        private static readonly MinimizeMode[] AvailableThemeModes =
        {
            MinimizeMode.HeartbrokenHero,
            MinimizeMode.Bollywood,
            MinimizeMode.SpaceHero,
            MinimizeMode.DigitalPet,
            MinimizeMode.OfficeWorker
        };

        private readonly DispatcherTimer _timer;
        private readonly PersianCalendar _persianCalendar = new PersianCalendar();

        private const double ExpandedWidth = 300;
        private const double ExpandedHeight = 78;

        private const double CollapsedWidth = 42;
        private const double CollapsedHeight = 52;

        private const double RightMargin = 8;
        private const double BottomMargin = 12;
        private const int DialogueDurationMilliseconds = 3600;
        private const int DialogueFadeDurationMilliseconds = 350;
        private const int FlightDurationMilliseconds = 700;
        private const int MinimizeAnimationDurationMilliseconds =
            DialogueDurationMilliseconds + FlightDurationMilliseconds;
        private const double HeroAnimationCanvasSize = 420;
        private const string ConfigFileName = "config.txt";

        private const string AppRegistryPath = @"Software\Calendra";
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupQuestionValueName = "StartupQuestionAsked";
        private const string RunValueName = "Calendra";

        private bool _isExpanded = false;
        private bool _stateBeforeMinimize = false;
        private bool _isRestoringFromMinimize = false;
        private bool _isAnimatingToTray = false;
        private bool _bodyDragCandidate = false;
        private bool _suppressBodyClick = false;
        private SW.Point _bodyDragStartPoint;
        private double _restoreLeft;
        private double _restoreTop;
        private MinimizeMode _selectedMinimizeMode = MinimizeMode.Random;
        private MinimizeMode? _lastRandomMode;
        private Window? _minimizeDialogueWindow;
        private Window? _finalToastWindow;
        private DispatcherTimer? _toastCloseTimer;
        private Forms.NotifyIcon? _notifyIcon;

        private static string ConfigFilePath =>
            Path.Combine(AppContext.BaseDirectory, ConfigFileName);

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

        private readonly (string Name, string TimeZoneId)[] _usTimeZones =
        {
            ("شرق آمریکا (نیویورک)", "Eastern Standard Time"),
            ("مرکز آمریکا (شیکاگو)", "Central Standard Time"),
            ("کوهستان (دنور)", "Mountain Standard Time"),
            ("آریزونا (فینیکس)", "US Mountain Standard Time"),
            ("غرب آمریکا (لس‌آنجلس)", "Pacific Standard Time"),
            ("آلاسکا (انکوریج)", "Alaskan Standard Time"),
            ("هاوایی (هونولولو)", "Hawaiian Standard Time")
        };

        public MainWindow()
        {
            InitializeComponent();

            LoadOrCreateMinimizeConfig();
            UpdateMinimizeSettingsUi();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };

            _timer.Tick += Timer_Tick;

            // همیشه در حالت باز شده اجرا شود
            _isExpanded = true;

            InitializeTrayIcon();
        }

        private void LoadOrCreateMinimizeConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    _selectedMinimizeMode = MinimizeMode.Random;
                    WriteMinimizeConfig();
                    return;
                }

                string? configuredValue = null;

                foreach (string line in File.ReadAllLines(ConfigFilePath))
                {
                    const string settingPrefix = "MinimizeMode=";

                    if (line.StartsWith(settingPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        configuredValue = line[settingPrefix.Length..].Trim();
                        break;
                    }
                }

                if (configuredValue != null &&
                    Enum.TryParse(configuredValue, true, out MinimizeMode parsedMode) &&
                    Enum.IsDefined(parsedMode))
                {
                    _selectedMinimizeMode = parsedMode;
                    return;
                }

                _selectedMinimizeMode = MinimizeMode.Random;
                WriteMinimizeConfig();
            }
            catch (Exception ex)
            {
                _selectedMinimizeMode = MinimizeMode.Random;

                WpfMessageBox.Show(
                    $"امکان خواندن یا ساخت {ConfigFileName} کنار برنامه وجود ندارد:\n{ex.Message}",
                    "تنظیمات Calendra",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void SaveMinimizeConfig()
        {
            try
            {
                WriteMinimizeConfig();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    $"تنظیم انتخاب شد، اما در {ConfigFileName} ذخیره نشد:\n{ex.Message}",
                    "تنظیمات Calendra",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void WriteMinimizeConfig()
        {
            File.WriteAllText(
                ConfigFilePath,
                $"MinimizeMode={_selectedMinimizeMode}{Environment.NewLine}");
        }

        private void UpdateMinimizeSettingsUi()
        {
            RandomModeRadio.IsChecked = _selectedMinimizeMode == MinimizeMode.Random;
            HeartbrokenHeroModeRadio.IsChecked =
                _selectedMinimizeMode == MinimizeMode.HeartbrokenHero;
            BollywoodModeRadio.IsChecked =
                _selectedMinimizeMode == MinimizeMode.Bollywood;
            SpaceHeroModeRadio.IsChecked =
                _selectedMinimizeMode == MinimizeMode.SpaceHero;
            DigitalPetModeRadio.IsChecked =
                _selectedMinimizeMode == MinimizeMode.DigitalPet;
            OfficeWorkerModeRadio.IsChecked =
                _selectedMinimizeMode == MinimizeMode.OfficeWorker;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDates();
            UpdateStartupButtonState();

            ExpandFromRightSide();

            _timer.Start();

            Dispatcher.BeginInvoke(
                new Action(AskStartupQuestionFirstTime),
                DispatcherPriority.ApplicationIdle);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateDates();

            if (WindowState != WindowState.Minimized && !_isAnimatingToTray)
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
            PersianDateText.Text = ToPersianDigits(persianDate);

            int gregorianYear = now.Year;
            int gregorianMonth = now.Month;
            int gregorianDay = now.Day;

            string gregorianMonthName = _gregorianMonthNames[gregorianMonth - 1];

            // تاریخ میلادی به صورت عددی
            string gregorianDateNumeric = $"{gregorianYear:0000}/{gregorianMonth:00}/{gregorianDay:00}";
            GregorianDateText.Text = gregorianDateNumeric;

            UpdateGregorianMonthsHelp(now);
            UpdateUsWeekCountdown();
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

        private void UpdateUsWeekCountdown()
        {
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            TimeZoneInfo iranTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
            TimeSpan iranOffset = iranTimeZone.GetUtcOffset(utcNow);
            string[] lines = new string[_usTimeZones.Length];

            for (int index = 0; index < _usTimeZones.Length; index++)
            {
                var zoneInfo = _usTimeZones[index];
                TimeZoneInfo usTimeZone =
                    TimeZoneInfo.FindSystemTimeZoneById(zoneInfo.TimeZoneId);
                DateTimeOffset localNow =
                    TimeZoneInfo.ConvertTime(utcNow, usTimeZone);

                // در تقویم رایج آمریکا، هفته در پایان شنبه تمام می‌شود.
                int daysUntilNextSunday = 7 - (int)localNow.DayOfWeek;
                DateTime localWeekEnd = DateTime.SpecifyKind(
                    localNow.Date.AddDays(daysUntilNextSunday),
                    DateTimeKind.Unspecified);
                DateTime weekEndUtc =
                    TimeZoneInfo.ConvertTimeToUtc(localWeekEnd, usTimeZone);
                TimeSpan remaining = weekEndUtc - utcNow.UtcDateTime;

                if (remaining < TimeSpan.Zero)
                    remaining = TimeSpan.Zero;

                TimeSpan iranDifference =
                    iranOffset - usTimeZone.GetUtcOffset(utcNow);

                string localTime =
                    ToPersianDigits(localNow.ToString("HH:mm", CultureInfo.InvariantCulture));
                string difference = FormatIranTimeDifference(iranDifference);
                string countdown = FormatRemainingTime(remaining);

                lines[index] =
                    $"{zoneInfo.Name}: ساعت {localTime}  |  {difference}  |  {countdown} مانده";
            }

            UsWeekCountdownText.Text = string.Join(Environment.NewLine, lines);
        }

        private static string FormatIranTimeDifference(TimeSpan difference)
        {
            TimeSpan absoluteDifference = difference.Duration();
            string hours =
                ToPersianDigits(((int)absoluteDifference.TotalHours).ToString(CultureInfo.InvariantCulture));
            string minutes =
                ToPersianDigits(absoluteDifference.Minutes.ToString("00", CultureInfo.InvariantCulture));
            string direction = difference >= TimeSpan.Zero ? "جلوتر" : "عقب‌تر";

            return $"ایران {hours}:{minutes} {direction}";
        }

        private static string FormatRemainingTime(TimeSpan remaining)
        {
            string days =
                ToPersianDigits(((int)remaining.TotalDays).ToString(CultureInfo.InvariantCulture));
            string hours =
                ToPersianDigits(remaining.Hours.ToString(CultureInfo.InvariantCulture));
            string minutes =
                ToPersianDigits(remaining.Minutes.ToString(CultureInfo.InvariantCulture));

            return $"{days} روز و {hours} ساعت و {minutes} دقیقه";
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
                Icon = GetApplicationIcon(),
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
                CloseMinimizeDialogueWindow();
                CloseFinalToastWindow();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            };
        }

        private static System.Drawing.Icon GetApplicationIcon()
        {
            string? executablePath = Environment.ProcessPath;

            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                System.Drawing.Icon? applicationIcon =
                    System.Drawing.Icon.ExtractAssociatedIcon(executablePath);

                if (applicationIcon != null)
                    return applicationIcon;
            }

            return System.Drawing.SystemIcons.Application;
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

            return GetScreenWorkAreaInWpfUnits(primaryScreen);
        }

        private Rect GetCurrentScreenWorkAreaInWpfUnits()
        {
            SW.Point windowCenter = PointToScreen(
                new SW.Point(ActualWidth / 2, ActualHeight / 2));
            Forms.Screen currentScreen = Forms.Screen.FromPoint(
                new System.Drawing.Point((int)windowCenter.X, (int)windowCenter.Y));

            return GetScreenWorkAreaInWpfUnits(currentScreen);
        }

        private Rect GetScreenWorkAreaInWpfUnits(Forms.Screen screen)
        {
            var workingArea = screen.WorkingArea;

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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMinimizeSettingsUi();
            SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
        }

        private void MinimizeModeRadio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radioButton ||
                radioButton.Tag is not string modeName ||
                !Enum.TryParse(modeName, true, out MinimizeMode selectedMode) ||
                !Enum.IsDefined(selectedMode))
            {
                return;
            }

            _selectedMinimizeMode = selectedMode;
            UpdateMinimizeSettingsUi();
            SaveMinimizeConfig();
            SettingsPopup.IsOpen = false;
        }

        private MinimizeTheme SelectMinimizeTheme()
        {
            MinimizeMode selectedMode = _selectedMinimizeMode;

            if (selectedMode == MinimizeMode.Random)
            {
                do
                {
                    selectedMode = AvailableThemeModes[
                        Random.Shared.Next(AvailableThemeModes.Length)];
                }
                while (_lastRandomMode == selectedMode && AvailableThemeModes.Length > 1);

                _lastRandomMode = selectedMode;
            }

            return GetMinimizeTheme(selectedMode);
        }

        private static MinimizeTheme GetMinimizeTheme(MinimizeMode mode)
        {
            return mode switch
            {
                MinimizeMode.Bollywood => new MinimizeTheme(
                    mode,
                    "فیلم هندیِ تقویمی",
                    "🌹",
                    "من می‌رم... اما خاطره‌ی تاریخ امروز همیشه باهات می‌مونه!",
                    "هر وقت پشیمون شدی، کنار ساعت ویندوز منتظرتم... 🎬",
                    "#FFFF4D8D",
                    "#FFFFB347",
                    "برو گلبرگاتو جمع کن",
                    1080),

                MinimizeMode.SpaceHero => new MinimizeTheme(
                    mode,
                    "ماموریت فضایی",
                    "🚀",
                    "۳... ۲... ۱... ظاهراً دیگه به من نیازی نیست!",
                    "ماموریت پایان یافت؛ قلب خلبان همچنان فعال است. 🛰️",
                    "#FF38BDF8",
                    "#FF6366F1",
                    "به مسیرت ادامه بده",
                    720),

                MinimizeMode.DigitalPet => new MinimizeTheme(
                    mode,
                    "حیوان خانگی دیجیتال",
                    "🐾",
                    "یعنی واقعاً می‌خوای منو تنها بذاری؟ 🥺",
                    "من همین پایین می‌مونم... شاید دوباره دلت برام تنگ شد 🐾",
                    "#FFC084FC",
                    "#FFF472B6",
                    "برو توی لونه‌ات",
                    540),

                MinimizeMode.OfficeWorker => new MinimizeTheme(
                    mode,
                    "کارمند مظلوم",
                    "☕",
                    "باشه رئیس... خودم می‌رم پایین.",
                    "حقوق نمی‌گیرم، مرخصی ندارم، تازه دوستم هم نداری! 😑",
                    "#FF34D399",
                    "#FFFBBF24",
                    "برو یه چایی بخور",
                    540),

                _ => new MinimizeTheme(
                    MinimizeMode.HeartbrokenHero,
                    "قهرمان دل‌شکسته",
                    "💔",
                    "باشه دیگه... ما رو دوست نداری؟ 🥺",
                    "درسته دوستم نداری... اما من همیشه همین‌جا توی قلبت هستم! 💔",
                    "#FFFF5D8B",
                    "#FF7C3AED",
                    "فعلاً قهر باش",
                    720)
            };
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            AnimateToTray();
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

        private void AnimateToTray()
        {
            if (_isAnimatingToTray)
                return;

            MinimizeTheme theme = SelectMinimizeTheme();
            _isAnimatingToTray = true;
            _stateBeforeMinimize = _isExpanded;
            _restoreLeft = Left;
            _restoreTop = Top;

            MonthsPopup.IsOpen = false;
            SettingsPopup.IsOpen = false;

            Rect workArea = GetCurrentScreenWorkAreaInWpfUnits();
            ShowMinimizeDialogue(theme, workArea);
            PrepareHeroAnimationCanvas();

            double targetLeft =
                workArea.Right - RightMargin - (HeroAnimationCanvasSize / 2);
            double targetTop =
                workArea.Bottom - BottomMargin - (HeroAnimationCanvasSize / 2);
            Duration flightDuration = new Duration(
                TimeSpan.FromMilliseconds(FlightDurationMilliseconds));
            IEasingFunction flightEasing = new CubicEase
            {
                EasingMode = EasingMode.EaseIn
            };

            DoubleAnimation leftAnimation = new DoubleAnimation(Left, targetLeft, flightDuration)
            {
                BeginTime = TimeSpan.FromMilliseconds(DialogueDurationMilliseconds),
                EasingFunction = flightEasing
            };
            DoubleAnimation topAnimation = new DoubleAnimation(Top, targetTop, flightDuration)
            {
                BeginTime = TimeSpan.FromMilliseconds(DialogueDurationMilliseconds),
                EasingFunction = flightEasing
            };

            DoubleAnimationUsingKeyFrames rotationAnimation =
                CreateHeroRotationAnimation(theme.HeroRotation);
            DoubleAnimationUsingKeyFrames scaleXAnimation =
                CreateHeroScaleAnimation();
            DoubleAnimationUsingKeyFrames scaleYAnimation =
                CreateHeroScaleAnimation();
            DoubleAnimationUsingKeyFrames opacityAnimation =
                CreateHeroOpacityAnimation();

            SWM.Color accentColor = ParseThemeColor(theme.AccentColor);
            MainShadow.Color = accentColor;

            DoubleAnimationUsingKeyFrames shadowOpacityAnimation =
                new DoubleAnimationUsingKeyFrames();
            shadowOpacityAnimation.KeyFrames.Add(
                new LinearDoubleKeyFrame(0.42, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            shadowOpacityAnimation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    0.95,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)),
                    new SineEase { EasingMode = EasingMode.EaseOut }));
            shadowOpacityAnimation.KeyFrames.Add(
                new LinearDoubleKeyFrame(
                    0,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(MinimizeAnimationDurationMilliseconds))));

            DoubleAnimationUsingKeyFrames shadowBlurAnimation =
                new DoubleAnimationUsingKeyFrames();
            shadowBlurAnimation.KeyFrames.Add(
                new LinearDoubleKeyFrame(22, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            shadowBlurAnimation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    42,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)),
                    new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }));
            shadowBlurAnimation.KeyFrames.Add(
                new LinearDoubleKeyFrame(
                    8,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(MinimizeAnimationDurationMilliseconds))));

            opacityAnimation.Completed += (_, _) =>
            {
                Hide();
                CloseMinimizeDialogueWindow();
                ClearMinimizeAnimations();
                RestoreAfterHeroAnimation();
                _isAnimatingToTray = false;
                ShowFinalToast(theme, workArea);
            };

            BeginAnimation(LeftProperty, leftAnimation);
            BeginAnimation(TopProperty, topAnimation);
            MainRotateTransform.BeginAnimation(
                SWM.RotateTransform.AngleProperty,
                rotationAnimation);
            MainScaleTransform.BeginAnimation(SWM.ScaleTransform.ScaleXProperty, scaleXAnimation);
            MainScaleTransform.BeginAnimation(SWM.ScaleTransform.ScaleYProperty, scaleYAnimation);
            MainBox.BeginAnimation(OpacityProperty, opacityAnimation);
            MainShadow.BeginAnimation(
                SWM.Effects.DropShadowEffect.OpacityProperty,
                shadowOpacityAnimation);
            MainShadow.BeginAnimation(
                SWM.Effects.DropShadowEffect.BlurRadiusProperty,
                shadowBlurAnimation);
        }

        private void PrepareHeroAnimationCanvas()
        {
            double horizontalPadding =
                (HeroAnimationCanvasSize - ExpandedWidth) / 2;
            double verticalPadding =
                (HeroAnimationCanvasSize - ExpandedHeight) / 2;

            MainBox.Width = ExpandedWidth;
            MainBox.Height = ExpandedHeight;
            MainBox.HorizontalAlignment = SW.HorizontalAlignment.Center;
            MainBox.VerticalAlignment = SW.VerticalAlignment.Center;

            Width = HeroAnimationCanvasSize;
            Height = HeroAnimationCanvasSize;
            Left = _restoreLeft - horizontalPadding;
            Top = _restoreTop - verticalPadding;
        }

        private void RestoreAfterHeroAnimation()
        {
            MainBox.ClearValue(WidthProperty);
            MainBox.ClearValue(HeightProperty);
            MainBox.HorizontalAlignment = SW.HorizontalAlignment.Stretch;
            MainBox.VerticalAlignment = SW.VerticalAlignment.Stretch;

            Width = _stateBeforeMinimize ? ExpandedWidth : CollapsedWidth;
            Height = _stateBeforeMinimize ? ExpandedHeight : CollapsedHeight;
            Left = _restoreLeft;
            Top = _restoreTop;
            UpdateLayoutForCurrentMode();
        }

        private static DoubleAnimationUsingKeyFrames CreateHeroRotationAnimation(
            double heroRotation)
        {
            DoubleAnimationUsingKeyFrames animation =
                new DoubleAnimationUsingKeyFrames();

            animation.KeyFrames.Add(
                new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    -12,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)),
                    new SineEase { EasingMode = EasingMode.EaseInOut }));
            animation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    heroRotation,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(980)),
                    new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 }));
            animation.KeyFrames.Add(
                new DiscreteDoubleKeyFrame(
                    heroRotation,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(DialogueDurationMilliseconds))));
            animation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    heroRotation + 180,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(MinimizeAnimationDurationMilliseconds)),
                    new CubicEase { EasingMode = EasingMode.EaseIn }));

            return animation;
        }

        private static DoubleAnimationUsingKeyFrames CreateHeroScaleAnimation()
        {
            DoubleAnimationUsingKeyFrames animation =
                new DoubleAnimationUsingKeyFrames();

            animation.KeyFrames.Add(
                new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    1.13,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)),
                    new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 }));
            animation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    1,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(980)),
                    new SineEase { EasingMode = EasingMode.EaseInOut }));
            animation.KeyFrames.Add(
                new DiscreteDoubleKeyFrame(
                    1,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(DialogueDurationMilliseconds))));
            animation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    0.08,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(MinimizeAnimationDurationMilliseconds)),
                    new CubicEase { EasingMode = EasingMode.EaseIn }));

            return animation;
        }

        private static DoubleAnimationUsingKeyFrames CreateHeroOpacityAnimation()
        {
            DoubleAnimationUsingKeyFrames animation =
                new DoubleAnimationUsingKeyFrames();

            animation.KeyFrames.Add(
                new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(
                new DiscreteDoubleKeyFrame(
                    1,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(DialogueDurationMilliseconds))));
            animation.KeyFrames.Add(
                new LinearDoubleKeyFrame(
                    0,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(MinimizeAnimationDurationMilliseconds))));

            return animation;
        }

        private void ClearMinimizeAnimations()
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            MainRotateTransform.BeginAnimation(SWM.RotateTransform.AngleProperty, null);
            MainScaleTransform.BeginAnimation(SWM.ScaleTransform.ScaleXProperty, null);
            MainScaleTransform.BeginAnimation(SWM.ScaleTransform.ScaleYProperty, null);
            MainBox.BeginAnimation(OpacityProperty, null);
            MainShadow.BeginAnimation(SWM.Effects.DropShadowEffect.OpacityProperty, null);
            MainShadow.BeginAnimation(SWM.Effects.DropShadowEffect.BlurRadiusProperty, null);

            MainRotateTransform.Angle = 0;
            MainScaleTransform.ScaleX = 1;
            MainScaleTransform.ScaleY = 1;
            MainBox.Opacity = 1;
            MainShadow.Color = SWM.Colors.Black;
            MainShadow.Opacity = 0.42;
            MainShadow.BlurRadius = 22;
        }

        private void ShowMinimizeDialogue(MinimizeTheme theme, Rect workArea)
        {
            CloseMinimizeDialogueWindow();

            SWM.Color accentColor = ParseThemeColor(theme.AccentColor);
            SWM.Color secondaryColor = ParseThemeColor(theme.SecondaryColor);

            SWM.LinearGradientBrush background = new SWM.LinearGradientBrush
            {
                StartPoint = new SW.Point(0, 0),
                EndPoint = new SW.Point(1, 1)
            };
            background.GradientStops.Add(
                new SWM.GradientStop(SWM.Color.FromArgb(250, 17, 24, 39), 0));
            background.GradientStops.Add(
                new SWM.GradientStop(
                    SWM.Color.FromArgb(
                        244,
                        secondaryColor.R,
                        secondaryColor.G,
                        secondaryColor.B),
                    1));

            Grid contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock iconText = new TextBlock
            {
                Text = theme.Icon,
                FontSize = 42,
                VerticalAlignment = SW.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(iconText, 0);

            StackPanel copyPanel = new StackPanel
            {
                FlowDirection = SW.FlowDirection.RightToLeft
            };
            copyPanel.Children.Add(new TextBlock
            {
                Text = theme.Title,
                Foreground = new SWM.SolidColorBrush(accentColor),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = SW.HorizontalAlignment.Right
            });
            copyPanel.Children.Add(new TextBlock
            {
                Text = theme.Dialogue,
                Foreground = SWM.Brushes.White,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = SW.TextAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(copyPanel, 1);

            contentGrid.Children.Add(iconText);
            contentGrid.Children.Add(copyPanel);

            Border dialogueBorder = new Border
            {
                Background = background,
                BorderBrush = new SWM.SolidColorBrush(accentColor),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(16, 13, 16, 13),
                Child = contentGrid,
                Effect = new SWM.Effects.DropShadowEffect
                {
                    Color = accentColor,
                    BlurRadius = 28,
                    ShadowDepth = 0,
                    Opacity = 0.62
                }
            };

            Window dialogueWindow = new Window
            {
                Width = 430,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = SWM.Brushes.Transparent,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Opacity = 0,
                Content = dialogueBorder
            };

            double targetLeft = Math.Clamp(
                Left + ((Width - dialogueWindow.Width) / 2),
                workArea.Left + 8,
                workArea.Right - dialogueWindow.Width - 8);
            dialogueWindow.Left = targetLeft;
            dialogueWindow.Top = Math.Max(workArea.Top + 8, Top - 128);
            dialogueWindow.Show();
            dialogueWindow.UpdateLayout();

            double targetTop = Top - dialogueWindow.ActualHeight - 14;

            if (targetTop < workArea.Top + 8)
            {
                targetTop = Math.Min(
                    workArea.Bottom - dialogueWindow.ActualHeight - 8,
                    Top + Height + 14);
            }

            dialogueWindow.Top = targetTop + 12;

            DoubleAnimation topAnimation = new DoubleAnimation(
                targetTop + 12,
                targetTop,
                TimeSpan.FromMilliseconds(340))
            {
                EasingFunction = new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.28
                }
            };

            DoubleAnimationUsingKeyFrames opacityAnimation =
                new DoubleAnimationUsingKeyFrames();
            opacityAnimation.KeyFrames.Add(
                new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacityAnimation.KeyFrames.Add(
                new EasingDoubleKeyFrame(
                    1,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)),
                    new SineEase { EasingMode = EasingMode.EaseOut }));
            opacityAnimation.KeyFrames.Add(
                new DiscreteDoubleKeyFrame(
                    1,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(
                        DialogueDurationMilliseconds - DialogueFadeDurationMilliseconds))));
            opacityAnimation.KeyFrames.Add(
                new LinearDoubleKeyFrame(
                    0,
                    KeyTime.FromTimeSpan(
                        TimeSpan.FromMilliseconds(DialogueDurationMilliseconds))));

            opacityAnimation.Completed += (_, _) =>
            {
                if (ReferenceEquals(_minimizeDialogueWindow, dialogueWindow))
                {
                    dialogueWindow.Close();
                }
            };
            dialogueWindow.Closed += (_, _) =>
            {
                if (ReferenceEquals(_minimizeDialogueWindow, dialogueWindow))
                {
                    _minimizeDialogueWindow = null;
                }
            };

            _minimizeDialogueWindow = dialogueWindow;
            dialogueWindow.BeginAnimation(Window.TopProperty, topAnimation);
            dialogueWindow.BeginAnimation(Window.OpacityProperty, opacityAnimation);
        }

        private void ShowFinalToast(MinimizeTheme theme, Rect workArea)
        {
            CloseFinalToastWindow();

            SWM.Color accentColor = ParseThemeColor(theme.AccentColor);
            SWM.Color secondaryColor = ParseThemeColor(theme.SecondaryColor);

            SWM.LinearGradientBrush background = new SWM.LinearGradientBrush
            {
                StartPoint = new SW.Point(0, 0),
                EndPoint = new SW.Point(1, 1)
            };
            background.GradientStops.Add(
                new SWM.GradientStop(SWM.Color.FromArgb(252, 15, 23, 42), 0));
            background.GradientStops.Add(
                new SWM.GradientStop(
                    SWM.Color.FromArgb(
                        246,
                        secondaryColor.R,
                        secondaryColor.G,
                        secondaryColor.B),
                    1));

            StackPanel toastContent = new StackPanel
            {
                FlowDirection = SW.FlowDirection.RightToLeft
            };
            toastContent.Children.Add(new TextBlock
            {
                Text = $"{theme.Icon}  {theme.Title}",
                Foreground = new SWM.SolidColorBrush(accentColor),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = SW.HorizontalAlignment.Right
            });
            toastContent.Children.Add(new TextBlock
            {
                Text = theme.FinalMessage,
                Foreground = SWM.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = SW.TextAlignment.Right,
                Margin = new Thickness(0, 8, 0, 4)
            });
            toastContent.Children.Add(new TextBlock
            {
                Text = "Calendra هنوز کنار ساعت ویندوز نشسته و به افق خیره شده...",
                Foreground = new SWM.SolidColorBrush(SWM.Color.FromRgb(191, 205, 235)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = SW.TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 10)
            });

            WrapPanel buttonPanel = new WrapPanel
            {
                HorizontalAlignment = SW.HorizontalAlignment.Right,
                FlowDirection = SW.FlowDirection.RightToLeft
            };
            Button restoreButton = CreateToastButton(
                "نه بابا، برگرد!",
                accentColor,
                true);
            Button dismissButton = CreateToastButton(
                theme.DismissButtonText,
                SWM.Color.FromRgb(71, 85, 105),
                false);
            Button settingsToastButton = CreateToastButton(
                "تنظیم شوخی‌ها ⚙",
                secondaryColor,
                false);
            buttonPanel.Children.Add(restoreButton);
            buttonPanel.Children.Add(dismissButton);
            buttonPanel.Children.Add(settingsToastButton);
            toastContent.Children.Add(buttonPanel);

            Border toastBorder = new Border
            {
                Background = background,
                BorderBrush = new SWM.SolidColorBrush(accentColor),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(18, 15, 18, 15),
                Child = toastContent,
                Effect = new SWM.Effects.DropShadowEffect
                {
                    Color = accentColor,
                    BlurRadius = 30,
                    ShadowDepth = 0,
                    Opacity = 0.58
                }
            };

            Window toastWindow = new Window
            {
                Width = 470,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = SWM.Brushes.Transparent,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Opacity = 0,
                Content = toastBorder
            };

            restoreButton.Click += (_, _) =>
            {
                toastWindow.Close();
                ShowFromTray();
            };
            dismissButton.Click += (_, _) => toastWindow.Close();
            settingsToastButton.Click += (_, _) =>
            {
                toastWindow.Close();
                ShowFromTray();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateMinimizeSettingsUi();
                    SettingsPopup.IsOpen = true;
                }), DispatcherPriority.ApplicationIdle);
            };

            toastWindow.Closed += (_, _) =>
            {
                if (ReferenceEquals(_finalToastWindow, toastWindow))
                {
                    _finalToastWindow = null;
                }

                _toastCloseTimer?.Stop();
                _toastCloseTimer = null;
            };

            _finalToastWindow = toastWindow;
            toastWindow.Left = workArea.Right + 12;
            toastWindow.Top = workArea.Bottom - 190;
            toastWindow.Show();
            toastWindow.UpdateLayout();

            double targetLeft = workArea.Right - toastWindow.ActualWidth - 16;
            double targetTop = workArea.Bottom - toastWindow.ActualHeight - 16;
            toastWindow.Top = targetTop;

            toastWindow.BeginAnimation(
                Window.LeftProperty,
                new DoubleAnimation(
                    workArea.Right + 12,
                    targetLeft,
                    TimeSpan.FromMilliseconds(520))
                {
                    EasingFunction = new BackEase
                    {
                        EasingMode = EasingMode.EaseOut,
                        Amplitude = 0.28
                    }
                });
            toastWindow.BeginAnimation(
                Window.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));

            _toastCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(9)
            };
            _toastCloseTimer.Tick += (_, _) =>
            {
                _toastCloseTimer?.Stop();

                if (ReferenceEquals(_finalToastWindow, toastWindow))
                {
                    toastWindow.Close();
                }
            };
            _toastCloseTimer.Start();
        }

        private static Button CreateToastButton(
            string text,
            SWM.Color backgroundColor,
            bool isPrimary)
        {
            return new Button
            {
                Content = text,
                Background = new SWM.SolidColorBrush(backgroundColor),
                Foreground = SWM.Brushes.White,
                BorderBrush = new SWM.SolidColorBrush(
                    isPrimary ? SWM.Colors.White : backgroundColor),
                BorderThickness = new Thickness(isPrimary ? 1 : 0),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(3),
                FontSize = 11,
                FontWeight = isPrimary ? FontWeights.Bold : FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        private void CloseMinimizeDialogueWindow()
        {
            Window? dialogueWindow = _minimizeDialogueWindow;
            _minimizeDialogueWindow = null;
            dialogueWindow?.Close();
        }

        private void CloseFinalToastWindow()
        {
            _toastCloseTimer?.Stop();
            _toastCloseTimer = null;

            Window? toastWindow = _finalToastWindow;
            _finalToastWindow = null;
            toastWindow?.Close();
        }

        private static SWM.Color ParseThemeColor(string colorValue)
        {
            return (SWM.Color)SWM.ColorConverter.ConvertFromString(colorValue)!;
        }

        private void ShowFromTray()
        {
            if (_isAnimatingToTray)
                return;

            CloseFinalToastWindow();
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

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _bodyDragCandidate = false;
            _suppressBodyClick = false;

            if (_isAnimatingToTray || e.ButtonState != MouseButtonState.Pressed)
                return;

            if (IsInsideButton(e.OriginalSource as DependencyObject))
                return;

            _bodyDragStartPoint = e.GetPosition(this);
            _bodyDragCandidate = true;
        }

        private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_bodyDragCandidate || e.LeftButton != MouseButtonState.Pressed)
                return;

            SW.Point currentPoint = e.GetPosition(this);
            double horizontalDistance = Math.Abs(currentPoint.X - _bodyDragStartPoint.X);
            double verticalDistance = Math.Abs(currentPoint.Y - _bodyDragStartPoint.Y);

            if (horizontalDistance < SystemParameters.MinimumHorizontalDragDistance &&
                verticalDistance < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _bodyDragCandidate = false;
            _suppressBodyClick = true;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // دکمهٔ ماوس ممکن است هم‌زمان با شروع DragMove رها شده باشد.
            }
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _bodyDragCandidate = false;

            if (_suppressBodyClick)
            {
                e.Handled = true;
                return;
            }

            if (IsInsideElement(e.OriginalSource as DependencyObject, ToggleHandle))
            {
                ToggleVisibility();
                e.Handled = true;
            }
        }

        private static bool IsInsideElement(
            DependencyObject? source,
            DependencyObject expectedAncestor)
        {
            DependencyObject? current = source;

            while (current != null)
            {
                if (ReferenceEquals(current, expectedAncestor))
                    return true;

                current = SWM.VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool IsInsideButton(DependencyObject? source)
        {
            DependencyObject? current = source;

            while (current != null)
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase)
                    return true;

                current = SWM.VisualTreeHelper.GetParent(current);
            }

            return false;
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
                    UpdateStartupButtonState();
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
            UpdateStartupButtonState();

            WpfMessageBox.Show(
                "اجرای خودکار Calendra با شروع ویندوز فعال شد.",
                "Calendra",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DisableStartup_Click(object sender, RoutedEventArgs e)
        {
            bool wasRemoved = DisableStartup();
            UpdateStartupButtonState();

            WpfMessageBox.Show(
                wasRemoved
                    ? "اجرای خودکار همین نسخه Calendra حذف شد."
                    : "این نسخه با آدرس فعلی در اجرای خودکار ثبت نیست؛ چیزی حذف نشد.",
                "Calendra",
                MessageBoxButton.OK,
                wasRemoved
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }

        // دکمه دوحالته افزودن یا حذف همین فایل اجرایی از استارتاپ
        private void AddToStartupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsCurrentExecutableRegisteredForStartup())
                {
                    MessageBoxResult removeResult = WpfMessageBox.Show(
                        "همین نسخه از برنامه با همین آدرس در اجرای خودکار ویندوز فعال است.\n\n" +
                        "آیا می‌خواهید از اجرای خودکار خارج شود؟",
                        "حذف از اجرای خودکار",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);

                    if (removeResult == MessageBoxResult.Yes)
                    {
                        bool wasRemoved = DisableStartup();
                        UpdateStartupButtonState();

                        WpfMessageBox.Show(
                            wasRemoved
                                ? "همین نسخه از اجرای خودکار ویندوز خارج شد."
                                : "مسیر ثبت‌شده تغییر کرده بود؛ برای امنیت چیزی حذف نشد.",
                            "اجرای خودکار",
                            MessageBoxButton.OK,
                            wasRemoved
                                ? MessageBoxImage.Information
                                : MessageBoxImage.Warning);
                    }

                    return;
                }

                MessageBoxResult addResult = WpfMessageBox.Show(
                    "این فایل اجرایی با آدرس فعلی در اجرای خودکار ویندوز ثبت نشده است.\n\n" +
                    "آیا می‌خواهید همین نسخه اضافه شود؟",
                    "افزودن به اجرای خودکار",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (addResult == MessageBoxResult.Yes)
                {
                    EnableStartup();
                    UpdateStartupButtonState();

                    WpfMessageBox.Show(
                        "همین نسخه با آدرس فعلی به اجرای خودکار ویندوز اضافه شد.",
                        "اجرای خودکار",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    "بررسی یا تغییر اجرای خودکار با خطا روبه‌رو شد:\n" + ex.Message,
                    "اجرای خودکار Calendra",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void UpdateStartupButtonState()
        {
            bool isCurrentVersionRegistered =
                IsCurrentExecutableRegisteredForStartup();

            AddToStartupButton.Background = new SWM.SolidColorBrush(
                isCurrentVersionRegistered
                    ? SWM.Color.FromArgb(95, 52, 211, 153)
                    : SWM.Color.FromArgb(50, 80, 200, 80));
            AddToStartupButton.ToolTip = isCurrentVersionRegistered
                ? "همین نسخه در اجرای خودکار فعال است؛ برای خارج‌کردن کلیک کنید"
                : "افزودن همین نسخه برنامه به اجرای خودکار ویندوز";
        }

        private static bool IsCurrentExecutableRegisteredForStartup()
        {
            string? currentExecutablePath = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(currentExecutablePath))
                return false;

            using RegistryKey? runKey =
                Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
            string? startupCommand = runKey?.GetValue(RunValueName) as string;
            string? registeredExecutablePath =
                ExtractExecutablePath(startupCommand);

            return AreSameExecutablePath(
                currentExecutablePath,
                registeredExecutablePath);
        }

        private static string? ExtractExecutablePath(string? startupCommand)
        {
            if (string.IsNullOrWhiteSpace(startupCommand))
                return null;

            string trimmedCommand = startupCommand.Trim();

            if (trimmedCommand.StartsWith('"'))
            {
                int closingQuoteIndex = trimmedCommand.IndexOf('"', 1);

                return closingQuoteIndex > 1
                    ? trimmedCommand[1..closingQuoteIndex]
                    : null;
            }

            int executableExtensionIndex = trimmedCommand.IndexOf(
                ".exe",
                StringComparison.OrdinalIgnoreCase);

            if (executableExtensionIndex >= 0)
            {
                return trimmedCommand[..(executableExtensionIndex + 4)];
            }

            int firstSpaceIndex = trimmedCommand.IndexOf(' ');

            return firstSpaceIndex > 0
                ? trimmedCommand[..firstSpaceIndex]
                : trimmedCommand;
        }

        private static bool AreSameExecutablePath(
            string? firstPath,
            string? secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) ||
                string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            try
            {
                string normalizedFirstPath = Path.GetFullPath(firstPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string normalizedSecondPath = Path.GetFullPath(secondPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                return string.Equals(
                    normalizedFirstPath,
                    normalizedSecondPath,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
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

        private static bool DisableStartup()
        {
            // اگر مقدار Registry به فایل دیگری اشاره کند، آن را دست‌کاری نکن.
            if (!IsCurrentExecutableRegisteredForStartup())
                return false;

            using RegistryKey? runKey =
                Registry.CurrentUser.CreateSubKey(RunRegistryPath);

            runKey?.DeleteValue(RunValueName, false);
            return true;
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
