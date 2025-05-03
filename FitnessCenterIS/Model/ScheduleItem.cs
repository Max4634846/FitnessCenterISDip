using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenterIS.Model
{
    public class ScheduleItem
    {
        public Schedules Schedule { get; set; }
        public string Color { get; set; }

        // Свойства для быстрого доступа к полям из Schedules
        public int ScheduleID => Schedule?.ScheduleID ?? 0;
        public string Title => Schedule?.Title ?? "";
        public DateTime? StartDateTime => Schedule?.StartDateTime;
        public DateTime? EndDateTime => Schedule?.EndDateTime;
        public string Note => Schedule?.Note;
        public string FormattedTimeRange => $"{StartDateTime:HH:mm} - {EndDateTime:HH:mm}";

        // Связанные данные
        public string RoomName => Schedule?.Rooms?.Name ?? "";
        public string TrainerName => Schedule?.Staffs?.Persons != null
            ? $"{Schedule.Staffs.Persons.Surname} {Schedule.Staffs.Persons.Name}"
            : "";
        public string ClientName => Schedule?.Clients?.Persons != null
            ? $"{Schedule.Clients.Persons.Surname} {Schedule.Clients.Persons.Name}"
            : "";
        public string GroupName => Schedule?.Groups?.Name ?? "";

        // Конструкторы
        public ScheduleItem(Schedules schedule, string color = "#3498db")
        {
            Schedule = schedule;
            Color = color;
        }

        // Метод для обновления базовой сущности Schedule
        public void UpdateSchedule(Schedules updatedSchedule)
        {
            Schedule = updatedSchedule;
        }
    }
}