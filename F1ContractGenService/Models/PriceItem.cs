using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace F1ContractGenService.Models
{
    public class PriceItem
    {
        public string CONO { get; set; }
        public string PRRF { get; set; }
        public string CUCD { get; set; }
        public string CUNO { get; set; }
        public string FVDT { get; set; }
        public string LVDT { get; set; }
        public string TX15 { get; set; }
        public string SCMO { get; set; } 
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

    }
}