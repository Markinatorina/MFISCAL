using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_DAL.Models
{
    public enum CurrencyTypes
    {
        Unknown = 0,

        // ISO 4217 currency codes
        EUR = 978,
        USD = 840,
        GBP = 826,
        JPY = 392,
        AUD = 36,
        CAD = 124,
        CHF = 756,
        CNY = 156,
        SEK = 752,
        NZD = 554,
        MXN = 484,
        SGD = 702,
        HKD = 344,
        NOK = 578,
        KRW = 410,
        INR = 356
    }
}
