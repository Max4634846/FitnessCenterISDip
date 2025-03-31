using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenterIS.Model
{
    public class ScheduleItem
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Instructor { get; set; }
        public string Color { get; set; } // Цвет для отображения в расписании
        public int CategoryID { get; set; } // Связь с категорией занятия
        public virtual Category Category { get; set; }

        public Schedules Schedule { get; set; }

        public ScheduleItem(Schedules schedule, string color = "#3498db")
        {
            Schedule = schedule;
            Color = color;
        }
    }
}
