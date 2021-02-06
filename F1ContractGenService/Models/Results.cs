using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace F1ContractGenService.Models
{
    public class Results
    {
        public Boolean Status { get; set; }
        public string OutputPath { get; set; }
        public string FileName { get; set; }
        public string Error { get; internal set; }
    }
}