using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenterIS.Model
{
    public class EmailRecipient
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public EmailRecipientType Type { get; set; }
        public int? SourceId { get; set; } // ID группы или услуги, к которой относится получатель

        public override string ToString()
        {
            switch (Type)
            {
                case EmailRecipientType.Group:
                    return $"Группа: {Name}";
                case EmailRecipientType.Individual:
                    return $"Клиент: {Name} ({Email})";
                case EmailRecipientType.AllGroups:
                    return "Все групповые занятия";
                default:
                    return Name;
            }
        }
    }

    public enum EmailRecipientType
    {
        Group,
        Individual,
        AllGroups
    }
}