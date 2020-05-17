using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Google.Apis.Calendar.v3.Data;
using System.Windows.Media;

namespace TrayClock
{
    namespace Calendar
    {
        class GoogleCalenderService : ICalendarService
        {
            public string AppName { get; set; }
            public string SecretsPath { get; set; }
            public string TokenPath { get; set; }

            CalendarService service;

            public async Task SetupAsync()
            {
                UserCredential credential;
                using (var stream = new FileStream(SecretsPath, FileMode.Open, FileAccess.Read))
                {
                    credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] { CalendarService.Scope.CalendarReadonly },
                        "user",
                        CancellationToken.None,
                        new FileDataStore(TokenPath, true));
                }

                service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = AppName
                });
            }

            public async Task<Calendar[]> GetCalendarsAsync()
            {
                CalendarListResource.ListRequest req = service.CalendarList.List();
                req.ShowDeleted = false;

                CalendarList src = await req.ExecuteAsync();
                var dst = new Calendar[src.Items.Count];
                for (int i = 0; i < src.Items.Count; i++)
                {
                    var calendar = src.Items[i];
                    Color col;
                    col.A = 255;
                    col.R = Convert.ToByte(calendar.BackgroundColor.Substring(1, 2), 16);
                    col.G = Convert.ToByte(calendar.BackgroundColor.Substring(3, 2), 16);
                    col.B = Convert.ToByte(calendar.BackgroundColor.Substring(5, 2), 16);
                    dst[i] = new Calendar()
                    {
                        color = col,
                        id = calendar.Id,
                        name = String.IsNullOrEmpty(calendar.SummaryOverride) ? calendar.Summary : calendar.SummaryOverride,
                        isHidden = false
                    };
                }
                return dst;
            }

            public async Task<Event[]> GetEventsAsync(Calendar calendar, DateTime startTime, DateTime endTime)
            {
                EventsResource.ListRequest request = service.Events.List(calendar.id);
                request.TimeMin = startTime;
                request.TimeMax = endTime;
                request.ShowDeleted = false;
                request.SingleEvents = true;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                Events src = await request.ExecuteAsync();
                var dst = new Event[src.Items.Count];
                for (int i = 0; i < src.Items.Count; i++)
                {
                    var ev = src.Items[i];
                    dst[i] = new Event()
                    {
                        subject = ev.Summary,
                        description = ev.Description,
                        uri = new Uri(ev.HtmlLink),
                        allDay = !String.IsNullOrEmpty(ev.Start.Date)
                    };
                    if (dst[i].allDay)
                    {
                        var start = new DateTime(int.Parse(ev.Start.Date.Substring(0, 4)), int.Parse(ev.Start.Date.Substring(5, 2)), int.Parse(ev.Start.Date.Substring(8, 2)));
                        var end = new DateTime(int.Parse(ev.End.Date.Substring(0, 4)), int.Parse(ev.End.Date.Substring(5, 2)), int.Parse(ev.End.Date.Substring(8, 2)));
                        Console.WriteLine($"Start:{start}");
                        Console.WriteLine($"End:{end}");
                        dst[i].startTime = start;
                        dst[i].duration = end.AddSeconds(-1) - start;
                    }
                    else
                    {
                        dst[i].startTime = ev.Start.DateTime.Value;
                        dst[i].duration = ev.End.DateTime.Value - ev.Start.DateTime.Value;
                    }
                }
                return dst;
            }
        }
    }
}