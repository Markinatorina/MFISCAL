using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_DAL.Models
{
    public class InvoiceDB : IIdentifiableDB
    {
        public Guid Id { get; set; }
        public required string InvoiceNumber { get; set; }
        public required long Amount { get; set; }
        public required CurrencyTypes Currency { get; set; }
        public required DateTimeOffset Created { get; set; }
        public required DateTimeOffset? Published { get; set; }
    }
}
