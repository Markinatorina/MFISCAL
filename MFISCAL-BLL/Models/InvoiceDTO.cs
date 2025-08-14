using MFISCAL_DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_BLL.Models
{
    public class InvoiceDTO
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public long Amount { get; set; } = 0;
        public CurrencyTypes Currency { get; set; } = CurrencyTypes.Unknown;
    }
}
