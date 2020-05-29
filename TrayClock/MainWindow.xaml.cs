using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace TrayClock
{
    class MainWindowData
    {
        public ReactiveProperty<DateTime> CalendarMonth { get; } = new ReactiveProperty<DateTime>(DateTime.Today);
        public ReactiveProperty<DateTime> CurrentTime { get; } = new ReactiveProperty<DateTime>(DateTime.Now);
    }

    struct EventData
    {
        public Color color;
        public string subject;
        public string description;
        public DateTime startTime;
        public TimeSpan duration;
        public bool allDay;
        public Uri uri;
    }

    class EventManager
    {
        Calendar.ICalendarService service;
        Dispatcher dispatcher;

        Calendar.Calendar[] calendars = null;
        Dictionary<DateTime, List<EventData>> eventLists = new Dictionary<DateTime, List<EventData>>();
        List<Task> taskList = new List<Task>();
        object eventListsLock = new object();
        SemaphoreSlim calendarsLock = new SemaphoreSlim(1, 1);

        public event EventHandler EventsUpdated;

        public EventManager(Calendar.ICalendarService service, Dispatcher dispatcher)
        {
            this.service = service;
            this.dispatcher = dispatcher;
        }

        public List<EventData> GetEventsInRange(DateTime start, DateTime end)
        {
            var list = new List<EventData>();
            var startMonth = new DateTime(start.Year, start.Month, 1);
            var endMonth = new DateTime(end.Year, end.Month, 1);
            for (var m = startMonth; m <= endMonth; m = m.AddMonths(1))
            {
                list.AddRange(GetEventsInMonth(m).FindAll((e) =>
                {
                    return (start <= e.startTime || start <= e.startTime + e.duration) &&
                            e.startTime <= end;
                }));
            }
            return list;
        }

        public List<EventData> GetEventsInMonth(DateTime month)
        {
            month = new DateTime(month.Year, month.Month, 1);
            List<EventData> list;
            bool hasVal = false;

            lock (eventListsLock)
            {
                hasVal = eventLists.TryGetValue(month, out list);
                if (!hasVal)
                {
                    eventLists.Add(month, new List<EventData>());
                }
            }

            if (hasVal)
            {
                return list;
            }
            else
            {
                var task = SyncAsync(month);
                taskList.Add(task);
                return new List<EventData>();
            }
        }

        public void ClearCache()
        {
            Task.WaitAll(taskList.ToArray());
            taskList.Clear();

            lock (eventListsLock)
            {
                calendars = null;
                eventLists.Clear();
            }
        }

        private async Task SyncAsync(DateTime month)
        {
            DateTime monthBegin = new DateTime(month.Year, month.Month, 1);
            DateTime monthEnd = monthBegin.AddMonths(1).AddTicks(-1);
            List<EventData> events = new List<EventData>();

            await calendarsLock.WaitAsync();
            try
            {
                if (calendars == null)
                {
                    calendars = await service.GetCalendarsAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                calendarsLock.Release();
            }

            foreach (var calendar in calendars)
            {
                foreach (var ev in await service.GetEventsAsync(calendar, monthBegin, monthEnd).ConfigureAwait(false))
                {
                    events.Add(new EventData()
                    {
                        subject = ev.subject,
                        description = ev.description,
                        color = calendar.color,
                        startTime = ev.startTime,
                        duration = ev.duration,
                        allDay = ev.allDay,
                        uri = ev.uri
                    });
                }
            }

            lock (eventListsLock)
            {
                eventLists[month] = events;
            }

            _ = dispatcher.BeginInvoke(() =>
              {
                  EventsUpdated(this, EventArgs.Empty);
              });
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        const string teamsCanceledText = "Canceled: ";
        const int calendarAppointmentRowCnt = 4;
        string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\TrayClock";

        private MainWindowData data = new MainWindowData();
        private DispatcherTimer updateTimer;
        private Calendar.ICalendarService service;
        private EventManager eventManager;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(new HwndSourceHook(WndProc));

                service = new Calendar.GoogleCalenderService()
                {
                    AppName = "TrayClock",
                    SecretsPath = appFolder + "\\credentials.json",
                    TokenPath = appFolder + "\\token"
                };
                service.SetupAsync();

                eventManager = new EventManager(service, this.Dispatcher);
                eventManager.EventsUpdated += Manager_EventsUpdated;

                UpdateCalendar();
            };

            DataContext = data;
            updateTimer = new DispatcherTimer()
            {
                Interval = new TimeSpan(0, 0, 0, 0, 100)
            };
            updateTimer.Tick += UpdateTimer_Tick;
        }

        private void Manager_EventsUpdated(object sender, EventArgs e)
        {
            UpdateCalendar();
        }

        public void Window_Show()
        {
            Topmost = true;
            Topmost = false;
            Activate();
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

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            var prev = data.CurrentTime.Value.Day;
            data.CurrentTime.Value = DateTime.Now;
            if (prev != data.CurrentTime.Value.Day)
            {
                DayChanged();
            }
        }

        private string DateTimeToString(DateTime time, bool allDay)
        {
            string str = "";
            if (time.Year != DateTime.Today.Year)
            {
                str += $"{time:yyyy/MM/dd}";
            }
            else
            {
                str += $"{time:MM/dd}";
            }
            if (!allDay)
            {
                str += $" {time:H:mm}";
            }
            return str;
        }

        void UpdateCalendar()
        {
            DateTime today = DateTime.Today;
            DateTime monthBegin = new DateTime(data.CalendarMonth.Value.Year, data.CalendarMonth.Value.Month, 1);
            DateTime calendarBegin = monthBegin.AddDays(-(int)monthBegin.DayOfWeek);
            DateTime monthEnd = monthBegin.AddMonths(1).AddTicks(-1);
            DateTime calendarEnd = monthEnd.AddDays(6 - (int)monthEnd.DayOfWeek);
            {
                DateTime cursorDate = calendarBegin;

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

                        if (cursorDate < monthBegin || cursorDate > monthEnd)
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
                } while (cursorDate <= calendarEnd);
            }

            {
                var appointments = eventManager.GetEventsInRange(calendarBegin, calendarEnd);
                appointments = appointments.Where((e) => { return !e.subject.StartsWith(teamsCanceledText); }).ToList();
                appointments.Sort((a, b) => b.duration.CompareTo(a.duration));//予定の長さを降順でソート
                appointments.Sort((a, b) => a.startTime.CompareTo(b.startTime));//予定の早さを降順でソート

                int cellColCnt = (calendarEnd - calendarBegin).Days + 1;
                bool[,] usedCell = new bool[cellColCnt, calendarAppointmentRowCnt];

                foreach (var appoint in appointments)
                {
                    var startTime = appoint.startTime;
                    var endTime = (appoint.startTime + appoint.duration).AddTicks(-1);
                    if ((startTime < calendarBegin && endTime < calendarBegin) ||
                        (startTime > calendarEnd))
                    {
                        continue;
                    }
                    var firstDate = startTime - startTime.TimeOfDay;
                    var lastDate = endTime - endTime.TimeOfDay;
                    var cellCol = Math.Max((firstDate - calendarBegin).Days, 0);
                    var cellWidth = Math.Min((lastDate - new DateTime(Math.Max(firstDate.Ticks, calendarBegin.Ticks))).Days + 1, cellColCnt - cellCol);
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
                            string toolTipText = appoint.subject;
                            if (!(appoint.allDay && cellWidth == 1))
                            {
                                toolTipText += $"\n{DateTimeToString(appoint.startTime, appoint.allDay)} - {DateTimeToString(appoint.startTime + appoint.duration, appoint.allDay)}";
                            }
                            TextBlock textBlock = new TextBlock()
                            {
                                Text = appoint.subject,
                                Background = new SolidColorBrush(appoint.color),
                                Foreground = new SolidColorBrush(((((appoint.color.R * 299) + (appoint.color.G * 587) + (appoint.color.B * 114)) / 1000) < 128) ? Colors.White : Colors.Black),
                                Style = (Style)Resources["AppointmentLabel"],
                                ToolTip = toolTipText
                            };
                            if (appoint.uri != null)
                            {
                                textBlock.PreviewMouseDown += (s, e) =>
                                {
                                    string url = appoint.uri.ToString();
                                    url = url.Replace("&", "^&");
                                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                                };
                            }
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

        private void NextMonthButton_Click(object sender, RoutedEventArgs e)
        {
            data.CalendarMonth.Value = data.CalendarMonth.Value.AddMonths(1);
            UpdateCalendar();
        }
        private void PrevMonthButton_Click(object sender, RoutedEventArgs e)
        {
            data.CalendarMonth.Value = data.CalendarMonth.Value.AddMonths(-1);
            UpdateCalendar();
        }
        private void CurrentMonthButton_Click(object sender, RoutedEventArgs e)
        {
            data.CalendarMonth.Value = DateTime.Today;
            UpdateCalendar();
        }
        private void UpdateCalendarButton_Click(object sender, RoutedEventArgs e)
        {
            eventManager.ClearCache();
            UpdateCalendar();
        }

        private void DayChanged()
        {
            eventManager.ClearCache();
            UpdateCalendar();
        }

        private void AcrylicWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            updateTimer.IsEnabled = IsVisible;
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
