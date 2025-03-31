using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenterIS.Model
{
    public class UsersCollection
    {
        public int UserID { get; set; }
        public Nullable<int> StaffID { get; set; }
        public string Password { get; set; }
        public string Login { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
    }
}
