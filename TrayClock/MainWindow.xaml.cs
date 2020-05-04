using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.ApplicationModel.Appointments;

namespace TrayClock
{
    class MainWindowData
    {
        public ReactiveProperty<DateTime> CalendarMonth { get; } = new ReactiveProperty<DateTime>(DateTime.Today);
        public ReactiveProperty<DateTime> CurrentTime { get; } = new ReactiveProperty<DateTime>(DateTime.Now);
    }

    struct AppointmentData
    {
        public Color color;
        public string subject;
        public DateTime startTime;
        public TimeSpan duration;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        const int calendarAppointmentRowCnt = 4;
        const string teamsAppointmentCanceled = "Canceled: ";

        private MainWindowData data = new MainWindowData();
        private DispatcherTimer updateTimer;
        private AppointmentStore appointStore;
        private List<AppointmentData> appointments = new List<AppointmentData>();

        private DateTime calendarBeginDate;//カレンダー始めの日
        private DateTime monthBeginDate;//月始めの日
        private DateTime monthEndDate;//月終わりの日
        private DateTime calendarEndDate;//カレンダー終わりの日

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {

                HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(new HwndSourceHook(WndProc));
            };

            DataContext = data;
            updateTimer = new DispatcherTimer()
            {
                Interval = new TimeSpan(0, 0, 0, 0, 100)
            };
            updateTimer.Tick += UpdateTimer_Tick;

            appointStore = AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AllCalendarsReadOnly).AsTask().Result;
        }

        public void Window_Show()
        {
            Topmost = true;
            Topmost = false;
            WindowSetPos();
            Show();
        }

        public void Window_Hide()
        {
            Hide();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WinAPI.WM_APP)
            {
                Debug.WriteLine("WM_APP");
                if (IsVisible)
                {
                    Window_Hide();
                }
                else
                {
                    Window_Show();
                }
                handled = true;
                return (IntPtr)1;
            }
            if (msg == WinAPI.WM_CLOSE)
            {
                Window_Hide();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void WindowSetPos()
        {
            Top = SystemParameters.WorkArea.Bottom - Height + 1;
            Left = SystemParameters.WorkArea.Right - Width + 1;
        }

        private void ChangeCalendarState()
        {
            DateTime month = data.CalendarMonth.Value;
            monthBeginDate = new DateTime(month.Year, month.Month, 1);
            calendarBeginDate = monthBeginDate.AddDays(-(int)monthBeginDate.DayOfWeek);
            monthEndDate = monthBeginDate.AddMonths(1).AddDays(-1);
            calendarEndDate = monthEndDate.AddDays(7 - (int)monthEndDate.DayOfWeek).AddTicks(-1);
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            var prev = data.CurrentTime.Value.Day;
            data.CurrentTime.Value = DateTime.Now;
            if (prev != data.CurrentTime.Value.Day)
            {
                DayChanged();
            }
        }

        void UpdateCalendar()
        {
            DateTime today = DateTime.Today;

            {
                DateTime cursorDate = calendarBeginDate;

                CalendarGrid.RowDefinitions.Clear();
                CalendarGrid.Children.Clear();
                int column = 0;
                int row = 0;
                //DayOfWeek
                CalendarGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                for (int d = 0; d < 7; d++)
                {
                    var label = new TextBlock()
                    {
                        Text = cursorDate.AddDays(d).ToString("ddd"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    Grid.SetRow(label, row);
                    Grid.SetColumn(label, column++);
                    CalendarGrid.Children.Add(label);
                }
                row++;
                do
                {
                    //Separator
                    column = 0;
                    CalendarGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                    var separator = new Separator();
                    CalendarGrid.Children.Add(separator);
                    Grid.SetRow(separator, row++);
                    Grid.SetColumnSpan(separator, 7);
                    //Day
                    column = 0;
                    CalendarGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                    for (int d = 0; d < 7; d++)
                    {
                        var label = new TextBlock()
                        {
                            Text = cursorDate.Day.ToString()
                        };

                        if (cursorDate < monthBeginDate || cursorDate > monthEndDate)
                        {
                            label.Style = (Style)Resources["DisabledLabel"];
                        }
                        else if (cursorDate == today)
                        {
                            label.Style = (Style)Resources["TodayLabel"];
                        }
                        Grid.SetRow(label, row);
                        Grid.SetColumn(label, column++);
                        CalendarGrid.Children.Add(label);
                        cursorDate = cursorDate.AddDays(1);
                    }
                    row++;
                    //Appointments
                    column = 0;
                    for (int i = 0; i < calendarAppointmentRowCnt; i++)
                    {
                        CalendarGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                        row++;
                    }
                } while (cursorDate <= calendarEndDate);
            }

            {
                appointments.Sort((a, b) => (int)(b.duration - a.duration).Ticks);//予定の長さを降順でソート
                appointments.Sort((a, b) => (int)(a.startTime - b.startTime).Ticks);//予定の早さを降順でソート

                int cellColCnt = (calendarEndDate - calendarBeginDate).Days + 1;
                bool[,] usedCell = new bool[cellColCnt, calendarAppointmentRowCnt];

                foreach (var appoint in appointments)
                {
                    var startTime = appoint.startTime;
                    var endTime = (appoint.startTime + appoint.duration).AddTicks(-1);
                    if ((startTime < calendarBeginDate && endTime < calendarBeginDate) ||
                        (startTime > calendarEndDate))
                    {
                        continue;
                    }
                    var firstDate = startTime - startTime.TimeOfDay;
                    var lastDate = endTime - endTime.TimeOfDay;
                    var cellCol = Math.Max((firstDate - calendarBeginDate).Days, 0);
                    var cellWidth = Math.Min((lastDate - new DateTime(Math.Max(firstDate.Ticks, calendarBeginDate.Ticks))).Days + 1, cellColCnt - cellCol);
                    int? cellRow = null;
                    for (int r = 0; r < calendarAppointmentRowCnt; r++)
                    {
                        int c;
                        for (c = 0; c < cellWidth; c++)
                        {
                            if (usedCell[cellCol + c, r])
                            {
                                break;
                            }
                        }
                        if (c == cellWidth)
                        {
                            cellRow = r;
                            break;
                        }
                    }
                    if (cellRow.HasValue)
                    {
                        for (int c = 0; c < cellWidth; c++)
                        {
                            usedCell[cellCol + c, cellRow.Value] = true;
                        }
                        int row = cellCol / 7;
                        int column = cellCol % 7;
                        for (int c = 0; c < cellWidth;)
                        {
                            int width = Math.Min(7 - column, cellWidth - c);
                            TextBlock textBlock = new TextBlock()
                            {
                                Text = appoint.subject,
                                Background = new SolidColorBrush(appoint.color),
                                Style = (Style)Resources["AppointmentLabel"],
                                ToolTip = appoint.subject
                            };
                            Grid.SetRow(textBlock, row * (calendarAppointmentRowCnt + 2) + 3 + cellRow.Value);
                            Grid.SetColumn(textBlock, column);
                            Grid.SetColumnSpan(textBlock, width);
                            CalendarGrid.Children.Add(textBlock);
                            row++;
                            column = 0;
                            c += width;
                        }
                    }
                }
            }
        }

        private async void NextMonthButton_Click(object sender, RoutedEventArgs e)
        {
            data.CalendarMonth.Value = data.CalendarMonth.Value.AddMonths(1);
            ChangeCalendarState();
            await UpdateAppointmentsAsync(calendarBeginDate, calendarEndDate - calendarBeginDate);
            UpdateCalendar();
        }
        private async void PrevMonthButton_Click(object sender, RoutedEventArgs e)
        {
            data.CalendarMonth.Value = data.CalendarMonth.Value.AddMonths(-1);
            ChangeCalendarState();
            await UpdateAppointmentsAsync(calendarBeginDate, calendarEndDate - calendarBeginDate);
            UpdateCalendar();
        }
        private async void CurrentMonthButton_Click(object sender, RoutedEventArgs e)
        {
            data.CalendarMonth.Value = DateTime.Today;
            ChangeCalendarState();
            await UpdateAppointmentsAsync(calendarBeginDate, calendarEndDate - calendarBeginDate);
            UpdateCalendar();
        }
        private async void UpdateCalendarButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateAppointmentsAsync(calendarBeginDate, calendarEndDate - calendarBeginDate);
            UpdateCalendar();
        }

        //appointmentsを更新
        private async Task UpdateAppointmentsAsync(DateTimeOffset rangeStart, TimeSpan rangeLength)
        {
            if (appointStore == null)
            {
                return;
            }
            appointments.Clear();
            var calendars = await appointStore.FindAppointmentCalendarsAsync();
            foreach (var calendar in calendars)
            {
                if (!calendar.IsHidden)
                {
                    Color calendarCol = Color.FromArgb(calendar.DisplayColor.A, calendar.DisplayColor.R, calendar.DisplayColor.G, calendar.DisplayColor.B);
                    foreach (var src in await calendar.FindAppointmentsAsync(rangeStart, rangeLength))
                    {
                        bool isCanceled = src.IsCanceledMeeting;
                        if (src.Subject.StartsWith(teamsAppointmentCanceled))
                        {
                            src.Subject = src.Subject.Remove(0, teamsAppointmentCanceled.Length);
                            isCanceled = true;
                        }
                        if (isCanceled)
                        {
                            continue;
                        }
                        AppointmentData dst = new AppointmentData()
                        {
                            subject = src.Subject,
                            color = calendarCol,
                            startTime = src.StartTime.DateTime,
                            duration = src.Duration
                        };
                        appointments.Add(dst);
                    }
                }
            }
        }

        private void DayChanged()
        {
            ChangeCalendarState();
            UpdateCalendar();
        }

        private async void AppointStore_StoreChanged(AppointmentStore sender, AppointmentStoreChangedEventArgs args)
        {
            ChangeCalendarState();
            await UpdateAppointmentsAsync(calendarBeginDate, calendarEndDate - calendarBeginDate);
        }

        private async void AcrylicWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            updateTimer.IsEnabled = IsVisible;
            if (IsVisible)
            {
                ChangeCalendarState();
                UpdateCalendar();
                await UpdateAppointmentsAsync(calendarBeginDate, calendarEndDate - calendarBeginDate);
                UpdateCalendar();
                if (appointStore != null)
                {
                    appointStore.StoreChanged += AppointStore_StoreChanged;
                }
            }
            else
            {
                if (appointStore != null)
                {
                    appointStore.StoreChanged -= AppointStore_StoreChanged;
                }
            }
        }

        private void AcrylicWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            WindowSetPos();
        }

        private void AcrylicWindow_LayoutUpdated(object sender, EventArgs e)
        {
            WindowSetPos();
        }

        private void AcrylicWindow_Deactivated(object sender, EventArgs e)
        {
            Window_Hide();
        }
    }
}
