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

        public int WaitingID => WaitingListClient?.WaitingID ?? 0;
        public int WaitingListID => WaitingListClient?.WaitingListID ?? 0;
        public int ClientID => WaitingListClient?.ClientID ?? 0;
        public DateTime? EnrollmentDateTime => WaitingListClient?.EnrollmentDateTime;
        // Безопасный доступ к IsProcessed
        public bool IsProcessed => WaitingListClient?.IsProcessed.HasValue == true &&
                                 WaitingListClient.IsProcessed.Value;
        public string Notes => WaitingListClient?.Notes ?? "";

        // Данные о клиенте и расписании через навигационные свойства
        public string ClientName => WaitingListClient?.Clients?.Persons != null
            ? $"{WaitingListClient.Clients.Persons.Surname} {WaitingListClient.Clients.Persons.Name}"
            : "";

        public string ServiceName => WaitingListClient?.WaitingLists?.SeasonticketServices?.Services?.Name ?? "";

        // Время занятия, на которое записан клиент в ожидании
        public DateTime? RequestedStartDateTime => WaitingListClient?.WaitingLists?.Schedules?.StartDateTime;
        public DateTime? RequestedEndDateTime => WaitingListClient?.WaitingLists?.Schedules?.EndDateTime;
        public string ScheduleTitle => WaitingListClient?.WaitingLists?.Schedules?.Title ?? "Нет информации";

        // Конструктор
        public WaitingListItem(WaitingListClients waitingListClient)
        {
            WaitingListClient = waitingListClient;
        }
    }
}
