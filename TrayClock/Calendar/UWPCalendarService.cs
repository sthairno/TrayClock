using Google.Apis.Calendar.v3.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.ApplicationModel.Appointments;

namespace TrayClock
{
    namespace Calendar
    {
        class UWPCalendarService : ICalendarService
        {
            private AppointmentStore appointStore;

            public async Task SetupAsync()
            {
                appointStore = await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AllCalendarsReadWrite).AsTask().ConfigureAwait(false);
            }

            public async Task<Calendar[]> GetCalendarsAsync()
            {
                if (appointStore == null)
                {
                    return new Calendar[0];
                }
                var src = await appointStore.FindAppointmentCalendarsAsync().AsTask().ConfigureAwait(false);
                var dst = new Calendar[src.Count];
                for (int i = 0; i < src.Count; i++)
                {
                    var calendar = src[i];
                    dst[i] = new Calendar()
                    {
                        id = calendar.LocalId,
                        color = Color.FromArgb(calendar.DisplayColor.A, calendar.DisplayColor.R, calendar.DisplayColor.G, calendar.DisplayColor.B),
                        name = calendar.DisplayName,
                        isHidden = calendar.IsHidden
                    };
                }
                return dst;
            }

            public async Task<Event[]> GetEventsAsync(Calendar calendar, DateTime startTime, DateTime endTime)
            {
                if (appointStore == null)
                {
                    return new Event[0];
                }
                var cal = await appointStore.GetAppointmentCalendarAsync(calendar.id).AsTask().ConfigureAwait(false);
                var src = await cal.FindAppointmentsAsync(startTime, endTime - startTime).AsTask().ConfigureAwait(false);
                var dst = new Event[src.Count];
                for (int i = 0; i < src.Count; i++)
                {
                    var ev = src[i];
                    if (ev.IsCanceledMeeting)
                    {
                        continue;
                    }
                    dst[i] = new Event()
                    {
                        subject = ev.Subject,
                        startTime = ev.StartTime.DateTime,
                        duration = ev.Duration,
                        allDay = ev.AllDay,
                        uri = String.IsNullOrEmpty(ev.OnlineMeetingLink) ? ev.Uri : new Uri(ev.OnlineMeetingLink)
                    };
                }
                return dst;
            }
        }
    }
}
