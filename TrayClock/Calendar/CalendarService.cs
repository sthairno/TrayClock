using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TrayClock
{
    namespace Calendar
    {
        class Event
        {
            public string subject;
            public string description;
            public DateTime startTime;
            public TimeSpan duration;
            public bool allDay;
            public Uri uri;
            public Color? color;
        }

        class Calendar
        {
            public string id;
            public Color color;
            public string name;
            public bool isHidden;
        }

        interface ICalendarService
        {
            Task SetupAsync();
            Task<Calendar[]> GetCalendarsAsync();
            Task<Event[]> GetEventsAsync(Calendar calendar, DateTime startTime, DateTime endTime);
        }
    }
}
