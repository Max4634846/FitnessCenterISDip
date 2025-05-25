using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenterIS.Model
{
    public class WaitingListItem
    {
        public WaitingListClients WaitingListClient { get; set; }

        // Базовые свойства с безопасной навигацией
        public int WaitingID => WaitingListClient?.WaitingID ?? 0;
        public int WaitingListID => WaitingListClient?.WaitingListID ?? 0;
        public int ClientID => WaitingListClient?.ClientID ?? 0;
        public DateTime? EnrollmentDateTime => WaitingListClient?.EnrollmentDateTime;
        public bool IsProcessed => WaitingListClient?.IsProcessed ?? false;
        public string Notes => WaitingListClient?.Notes ?? "";

        // Навигационные свойства со сложными проверками на null
        public string ClientName
        {
            get
            {
                try
                {
                    var client = WaitingListClient?.Clients;
                    if (client == null) return "Нет данных";

                    var person = client.Persons;
                    if (person == null) return "Нет данных о человеке";

                    return $"{person.Surname ?? ""} {person.Name ?? ""}".Trim();
                }
                catch
                {
                    return "Ошибка данных";
                }
            }
        }

        public string ServiceName
        {
            get
            {
                try
                {
                    if (WaitingListClient?.WaitingLists == null) return "Нет данных";
                    if (WaitingListClient.WaitingLists.SeasonticketServices == null) return "Нет абонемента";
                    if (WaitingListClient.WaitingLists.SeasonticketServices.Services == null) return "Нет услуги";

                    return WaitingListClient.WaitingLists.SeasonticketServices.Services.Name ?? "Без названия";
                }
                catch
                {
                    return "Ошибка данных";
                }
            }
        }

        public DateTime? RequestedStartDateTime
        {
            get
            {
                try
                {
                    if (WaitingListClient?.WaitingLists == null) return null;
                    if (WaitingListClient.WaitingLists.Schedules == null) return null;

                    return WaitingListClient.WaitingLists.Schedules.StartDateTime;
                }
                catch
                {
                    return null;
                }
            }
        }

        public DateTime? RequestedEndDateTime
        {
            get
            {
                try
                {
                    if (WaitingListClient?.WaitingLists == null) return null;
                    if (WaitingListClient.WaitingLists.Schedules == null) return null;

                    return WaitingListClient.WaitingLists.Schedules.EndDateTime;
                }
                catch
                {
                    return null;
                }
            }
        }

        public string ScheduleTitle
        {
            get
            {
                try
                {
                    if (WaitingListClient?.WaitingLists == null) return "Нет данных";
                    if (WaitingListClient.WaitingLists.Schedules == null) return "Нет расписания";

                    return WaitingListClient.WaitingLists.Schedules.Title ?? "Без названия";
                }
                catch
                {
                    return "Ошибка данных";
                }
            }
        }

        // Конструктор с проверкой на null
        public WaitingListItem(WaitingListClients waitingListClient)
        {
            WaitingListClient = waitingListClient ?? throw new ArgumentNullException(nameof(waitingListClient));
        }
    }
}
