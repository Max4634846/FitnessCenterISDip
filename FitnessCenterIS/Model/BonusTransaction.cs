using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenterIS.Model
{
    public partial class BonusTransaction
    {
        public int TransactionID { get; set; }
        public int ClientID { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } // "Accrual" or "Withdrawal"
        public DateTime DateTime { get; set; }
        public string Note { get; set; }

        public virtual Clients Clients { get; set; }
    }
}
