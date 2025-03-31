using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenterIS.Model
{
    public class Category
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Color { get; set; } // Цвет по умолчанию для этой категории
    }
}
