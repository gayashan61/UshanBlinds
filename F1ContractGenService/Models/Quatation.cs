using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace F1ContractGenService.Models
{
    public class Quatation
    {
        public string Owner { get; set; }
        public string ClientName { get; set; }
        public string ClientContact1 { get; set; }
        public string ClientContact2 { get; set; }
        public string ClientEmail1 { get; set; }
        public string ClientEmail2 { get; set; }
        public string ClientAddress1 { get; set; }
        public string ClientAddress2 { get; set; }
        public string ClientAddress3 { get; set; }
        public string ClientAddress4 { get; set; }
        public string JobNumber { get; set; }
        public string JobAddress1 { get; set; }
        public string JobAddress2 { get; set; }
        public string JobAddress3 { get; set; }
        public string JobAddress4 { get; set; }
        public string SiteName { get; set; }

        public string SubTotal { get; set; }
        public string GST { get; set; }
        public string Total { get; set; }
        public string Deposite { get; set; }
        public string Balance { get; set; }
        public string IssueDate { get; set; }
        public string EndDate { get; set; }

        public List<QuatationLines> QuatationLines { get; set; }
    }

    public class QuatationLines
    {
        public string No { get; set; }
        public string Code { get; set; }
        public string Category { get; set; }
        public string TrueDrop { get; set; }
        public string TrueWidth { get; set; }
        public string Drop { get; set; }
        public string Width { get; set; } 
        public string Range { get; set; } 
        public string Colour { get; set; } 
        public string RL { get; set; }
        public string StackingDetails { get; set; }
        public string Description { get; set; }
        public string Price { get; set; }
        public string Margin { get; set; }
         
    }
}