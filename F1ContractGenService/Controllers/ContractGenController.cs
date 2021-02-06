using F1ContractGenService.Models;
using Infor.M3.MvxSock;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Results;

namespace F1ContractGenService.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class ContractGenController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [Route("ForestOneDataService/ContractGen/GetContract")]
        [HttpPost]
        public JsonResult<Results> GetData(JObject jsonData)
        {
            Results res = new Results();
            try
            {
                string CUNO = jsonData.Value<string>("CUNO").ToString().ToUpper();
                string InDate = jsonData.Value<string>("InDate").ToString();
                if (InDate.Length == 7) {
                    InDate = "0" + InDate;
                }

                res = getContract(CUNO, InDate,"",""); 
                
            }
            catch (Exception ex)
            {
                res.OutputPath = "<<3>>"+ ex.Message;
                res.Status = false;
            }
            return Json<Results>(res);


        }


        [Route("ForestOneDataService/ContractGen/TESTAPI")]
        [HttpPost]
        public JsonResult<string> TESTAPI(JObject jsonData)
        {
            try
            {
                string CUNM = jsonData.Value<string>("CUNM").ToString();
                logger.Info("Hell  "+CUNM+" You have visited the Index view" + Environment.NewLine + DateTime.Now);
              


                return Json<string>("API SUCESS"+CUNM.ToUpper());
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        [Route("ForestOneDataService/ContractGen/GetCustomerList")]
        [HttpPost]
        public JsonResult<List<Customer>> GetCustomerList(JObject jsonData)
        {
            try
            {
                string CUNM = jsonData.Value<string>("CUNM").ToString();
                List<Customer> lstCustomers = GetCutomerList(CUNM);


                return Json<List<Customer>>(lstCustomers);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        [Route("ForestOneDataService/ContractGen/GetCustomerListByCUNO")]
        [HttpPost]
        public JsonResult<List<Customer>> GetCustomerListByCUNO(JObject jsonData)
        {
            try
            {
                string CUNM = jsonData.Value<string>("CUNO").ToString();


                List<Customer> lstCustomers = GetCutomerList(CUNM, true);


                return Json<List<Customer>>(lstCustomers);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private Results getContract(string CUNO, string InDate, string StartMonth, string WHLO) 
        {
            logger.Info("Level2 - FN getContract 1");
            Results results = new Results();
            string APIUser = ConfigurationManager.AppSettings["USER"];
            string APIPassword = ConfigurationManager.AppSettings["PASSWORD"];

            List<PriceItem> objPriceListLoose = new List<PriceItem>();
            List<PriceItem> objPriceListPack = new List<PriceItem>();
            List<PriceItem> objPriceListNationalCustomer = new List<PriceItem>();

            logger.Info("Level2 - Calling GetCustomerFullName 2");
            string CustomerFullName = GetCustomerFullName(CUNO, APIUser, APIPassword);
            logger.Info("Level2 - Calling CustomerFullName 3"+ CustomerFullName);
            bool IsNationalListCustomer = true;
            //Step 1
            string PRRF = this.GetPRRF(CUNO);

            if (string.IsNullOrEmpty(PRRF))
            {
                IsNationalListCustomer = false;

            }

            //Step 2
            if (IsNationalListCustomer)
            {
                objPriceListNationalCustomer = GetPriceList(CUNO, InDate, PRRF, APIUser, APIPassword, IsNationalListCustomer, "");
            }
            else
            {
                // If it was not a national contract then we need to make a two sets of MI calls to OIS017MI one for loose and one for pack using the two inputs below
                //  If the sql returns no results in step 1 the the customer is not on a national contract and we need to do some further logic to determine the inputs for the next step:
                PRRF = GetPRRFforNonNationalContractCustomers(CUNO, APIUser, APIPassword);

                //If it was not a national contract then we need to make a two sets of MI calls to OIS017MI one for loose and one for pack using the two inputs below:

                //For Lose Pack Get the calue from the table split
                string[] extractDim = PRRF.Split(',');
                string[] extractDimLoose = extractDim[0].Split('-');
                string[] extractDimPack = extractDim[1].Split('-');

                //Get Pricelist for Loose
                if (extractDimLoose[0].Trim().Equals("Loose"))
                {
                    string _prrf = extractDimLoose[1].Trim();
                    objPriceListLoose = GetPriceList(CUNO, InDate,  _prrf, APIUser, APIPassword, IsNationalListCustomer, extractDimLoose[1].Trim());
                }
                //Get Price Lst for Pack
                if (extractDimPack[0].Trim().Equals("Pack"))
                {
                    string _prrf = extractDimPack[1].Trim();
                    objPriceListPack = GetPriceList(CUNO, InDate, _prrf, APIUser, APIPassword, IsNationalListCustomer, extractDimPack[1].Trim());
                }
            }

            //Step 3  -- 3.	Then using these two inputs we can use OIS017MI – LstBasePrice to get the list of items and prices that are to be displayed on the pdf contract.
            /* LstPriceList and entering the input from the previous step in to the PRRF field if it was a national contract
               This will return a list off all of the pricelists that relate to this PRRF value, we need to look through these and find the one that is valid given the current date using the From and To Date
               We need to take note of the validfrom (FVDT) date of the pricelist that is valid **/
            List<BasePriceItem> priceListNational = new List<BasePriceItem>();
            List<BasePriceItem> priceListLeft = new List<BasePriceItem>();
            List<BasePriceItem> priceListRight = new List<BasePriceItem>();
            if (IsNationalListCustomer)
            {
                PriceItem LeftPriceItem = new PriceItem();
                LeftPriceItem = objPriceListNationalCustomer.First();
                priceListNational = LstBasePrice(LeftPriceItem.PRRF, LeftPriceItem.CUNO, LeftPriceItem.CUCD, LeftPriceItem.FVDT, APIUser, APIPassword);
            }
            else
            {
                //Take the Loose for Left side
                PriceItem LeftPriceItem = new PriceItem();
                if (objPriceListLoose.Count > 0)
                {
                    LeftPriceItem = objPriceListLoose.First();
                    priceListLeft = LstBasePrice(LeftPriceItem.PRRF, LeftPriceItem.CUNO, LeftPriceItem.CUCD, LeftPriceItem.FVDT, APIUser, APIPassword);
                }

                //Take the Pack for Right side
                PriceItem RightPriceItem = new PriceItem();
                if (objPriceListPack.Count > 0)
                {
                    RightPriceItem = objPriceListPack.First();
                    priceListRight = LstBasePrice(RightPriceItem.PRRF, RightPriceItem.CUNO, RightPriceItem.CUCD, RightPriceItem.FVDT, APIUser, APIPassword);
                }

            }

            //Step 4 - Generate the PDF
            logger.Info("Level2 - Generate the PDF 10");

            //Input 
            if (IsNationalListCustomer)
            {
              results=   GeneratePDF(priceListNational, IsNationalListCustomer, false, InDate, InDate, CustomerFullName,null,null, CUNO, StartMonth, WHLO);
            }
            else
            {

                if (priceListLeft.Count > 0 && priceListRight.Count > 0)
                {
                    results = GeneratePDF(null, IsNationalListCustomer, true, InDate, InDate, CustomerFullName, priceListLeft, priceListRight, CUNO, StartMonth, WHLO);
                }
                else if (priceListRight.Count > 0)
                {
                    results = GeneratePDF(null, IsNationalListCustomer, false, InDate, InDate, CustomerFullName, null, priceListRight, CUNO, StartMonth, WHLO);

                }
                else if (priceListLeft.Count > 0)
                {
                    results = GeneratePDF(null, IsNationalListCustomer, false, InDate, InDate, CustomerFullName, priceListLeft,null, CUNO, StartMonth, WHLO);

                }

            }

            logger.Info("Level2 - results.OutputPath 11");

            if (results.OutputPath == null)
            {
                logger.Info("Level2 - results.OutputPath is null 12");
                results = GeneratePDFNull(InDate, InDate, CustomerFullName, CUNO);

                logger.Info("Level2 - results.OutputPath is null 12" + results.OutputPath);
            }
            else {
                logger.Info("Level2 - results.OutputPath 13 "+ results.OutputPath);
            }

            return results;

        }

        private Results GeneratePDF(List<BasePriceItem> priceListNational, bool isNationalCus, bool isBothPriceList, string FromDate, string ToDate, string customerName, List<BasePriceItem> priceListLeft, List<BasePriceItem> priceListRight, string CUNO, string StartMonth, string WHLO)
        {
            Results results = new Results();
            String ContractEndDate = "";
            try
            {

                string PDFSavePath = ConfigurationManager.AppSettings.Get("PDFSavePath");
                
                PDFSavePath = PDFSavePath + WHLO +"\\";

                 bool exists = System.IO.Directory.Exists(PDFSavePath);

                if (!exists)
                    System.IO.Directory.CreateDirectory(PDFSavePath);

                DateTime newDate = DateTime.ParseExact(ToDate, "ddMMyyyy", CultureInfo.InvariantCulture);

                string reportDate = "";
                if (StartMonth.Trim().Length > 0)
                {
                      reportDate = newDate.ToString("dd X yyyy");
                    reportDate = reportDate.Replace("X", StartMonth);
                }
                else {
                      reportDate = newDate.ToString("dd MMMM yyyy");

                }


                string row = "";
                string rowFormat = "<tr><td style='padding-left:3px;'>{{NO}}</td><td style='padding-left:3px;'>{{A}}</td><td style='padding-left:3px;'>{{B}}</td><td style='text-align: right; padding-right:3px;'>{{C}}</td><td style='text-align: center;'>{{D}}</td><td style='text-align: right; padding-right:3px;'>{{E}}</td><td style='text-align: center;'>{{F}}</td></tr> ";
                string STDPriceTag = "Refer to Std Price List";


                if (isNationalCus)
                {
                    int counter = 1;
                    foreach (BasePriceItem bsItem in priceListNational)
                    {
                        string temp = rowFormat;
                        ContractEndDate = bsItem.LVDT;
                        temp = temp.Replace("{{NO}}", counter+"");
                        temp = temp.Replace("{{A}}", bsItem.ITNO);
                        temp = temp.Replace("{{B}}", bsItem.ITDS);
                        temp = temp.Replace("{{C}}", STDPriceTag);
                        temp = temp.Replace("{{D}}", bsItem.SPUN);
                        temp = temp.Replace("{{E}}", "$" + double.Parse(bsItem.SAPR).ToString("0.00", CultureInfo.InvariantCulture));
                        temp = temp.Replace("{{F}}", Regex.Replace(bsItem.MXID, "[^0-9.]", ""));

                        counter = counter + 1;
                        row = row + temp;
                    }
                }
                else
                {

                    List<BasePriceItem> FinalList = new List<BasePriceItem>();
                    if ((priceListLeft != null && priceListLeft.Count > 0))
                    {
                        foreach (BasePriceItem item in priceListLeft)
                        {
                            ContractEndDate = item.LVDT;
                            item.LeftPrice = "$" + double.Parse(item.SAPR).ToString("0.00", CultureInfo.InvariantCulture);
                            if (item.RightPrice == null) item.RightPrice = STDPriceTag;
                            FinalList.Add(item);

                        }
                    }

                    if ((priceListRight != null && priceListRight.Count > 0))
                    {

                        foreach (BasePriceItem item in priceListRight)
                        {
                            ContractEndDate = item.LVDT;
                            bool isDuplicateFound = false;
                            foreach (BasePriceItem baseItem in FinalList)
                            {
                                if (baseItem.ITNO.Trim().Equals(item.ITNO.Trim()))
                                {
                                    isDuplicateFound = true;

                                     

                                    baseItem.RightPrice = "$" + double.Parse(item.SAPR).ToString("0.00", CultureInfo.InvariantCulture);
                                    baseItem.MXID = item.MXID;
                                }
                            }

                            if (!isDuplicateFound)
                            {
                                item.RightPrice = "$" + double.Parse(item.SAPR).ToString("0.00", CultureInfo.InvariantCulture);
                                if (item.LeftPrice == null) item.LeftPrice = STDPriceTag;
                                FinalList.Add(item);
                            }



                        }

                    }





                    int counter = 1;
                    foreach (BasePriceItem bsItem in FinalList)
                    {
                        string temp = rowFormat;

                        temp = temp.Replace("{{NO}}", counter + "");
                        temp = temp.Replace("{{A}}", bsItem.ITNO);
                        temp = temp.Replace("{{B}}", bsItem.ITDS);
                        temp = temp.Replace("{{C}}", bsItem.LeftPrice);
                        temp = temp.Replace("{{D}}", bsItem.SPUN);
                        temp = temp.Replace("{{E}}", bsItem.RightPrice);
                        temp = temp.Replace("{{F}}", Regex.Replace(bsItem.MXID, "[^0-9.]", ""));

                        row = row + temp;
                        counter++;
                    }
                     

                }

                //Date Conver for Contract End Date 
                DateTime tempDateFormat = DateTime.ParseExact(ContractEndDate, "yyyyMMdd", CultureInfo.InvariantCulture);
                ContractEndDate = tempDateFormat.ToString("dd MMMM yyyy");

                string tableHeader = "<tr>" +
                    "<td style='width: 3%; border-style: none;padding-left:3px;'><strong>No.</strong></td>" +
                    "<td style='width: 17%; border-style: none;padding-left:3px;'><strong>PRODUCT CODE</strong></td>" +
         "<td style='width: 40%; border-style: none;padding-left:3px;'><strong>PRODUCT DESCRIPTION</strong></td>" +
         "<td style='width: 12%; border-style: none; text-align: right; padding-right:3px;'><strong>Price Ex GST Loose</strong></td> " +
         "<td style='width: 8%; border-style: none;text-align: center;'><strong>UOM</strong></td> " +
         "<td style='width: 12%; border-style: none;text-align: right; padding-right:3px;'><strong>Price Ex GST Pack</strong></td> " +
         "<td style='width: 8%; border-style: none;text-align: center;'><strong>PCK QTY</strong></td></tr>";

                var htmlContent = String.Format("<p><img style='display: block; margin-left: auto; margin-right: auto;' src='https://res.cloudinary.com/gunnersen/image/upload/v1599465601/systemLogos/F1LOGO.jpg' alt='smiley' /></p><h1 style='text-align: center; font-family:Arial;margin-top:-10px'>Contract Price List</h1><h4 style='margin-top:-8px;text-align: center;font-family:Arial;'>" + customerName + "</h4> <div style='text-align:center;font-family:Arial; margin-top: -10px'><span> Effective from:  </span> <span style = 'text-align: center;font-family:Arial; font-weight:bold' > " + reportDate + " </span> <span> to </span> <span style = 'text-align: center;font-family:Arial; font-weight:bold' > " + ContractEndDate + " </span></div>  <table style='margin-top:8px;border-color: #99b836; width: 100%;border-style: solid; border-collapse: collapse;font-size: 9px;font-family:Arial;' border='1' cellpadding='4'><tbody>" +
                    tableHeader +
                  "" + row + "" +
                   "</tbody></table><p style='font-size:9px; font-family:Arial;'><strong>Payment Terms and Conditions:</strong></p>" +
                   "<p style='font-size:small; font-family: Arial;font-size:9px; '>This product quotation and all orders placed with Forest One Australia Pty Ltd are subject to Forest One&rsquo;s Terms and Conditions of Trade which are available on written request or may be viewed at <a href='http://www.forest1.com/terms-and-conditions-3.html'>http://www.forest1.com/terms-and-conditions-3.html</a> ('Forest One Australia Terms'); By accepting this product quotation and/or by placing any order with Forest One Australia Pty Ltd you agree to be bound by Forest One&rsquo;s Terms.</p>" +
                   " ");


                string OutputPath = string.Empty;
                string FileName = string.Empty;
                try
                {
                    var htmlToPdf = new NReco.PdfGenerator.HtmlToPdfConverter();
                    string timestamp = Guid.NewGuid().ToString("N");
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();


                    FileName = "Contract-"+ WHLO + "-"+ CUNO + "-" + timestamp + ".pdf";
                    OutputPath =  PDFSavePath + FileName;
                    htmlToPdf.GeneratePdf(htmlContent, "", OutputPath);

                    results.Status = true;
                    results.OutputPath = OutputPath;
                    results.FileName = FileName;
                    logger.Info("Level3 - PDF File Generate Completed" + FileName);
                }
                catch (Exception ex)
                {
                    logger.Info("Level3 - PDF File Generate Error Y " + ex.StackTrace);
                    results.Status = false;
                    results.OutputPath = OutputPath + "<<1>>" + ex.Message;
                }


                return results;

            }
            catch (Exception ex)
            {
                logger.Info("Level3 - PDF File Generate Error X " + ex.StackTrace);
                results.Status = false;
                results.OutputPath = string.Empty;
                results.Error = "<<2>>" + ex.Message;
                return results;
            }

        }

        private Results GeneratePDFNull( string FromDate, string ToDate, string customerName,    string CUNO)
        {
            Results results = new Results();
            String ContractEndDate = "";
            try
            {

                string PDFSavePath = ConfigurationManager.AppSettings.Get("PDFSavePath");
                DateTime newDate = DateTime.ParseExact(ToDate, "ddMMyyyy", CultureInfo.InvariantCulture);
                string reportDate = newDate.ToString("dd MMMM yyyy");

                string row = "";
                string rowFormat = "<tr><td style='padding-left:3px;'> </td><td style='padding-left:3px;'> </td><td style='padding-left:3px;'>No Data Found</td><td style='text-align: right; padding-right:3px;'> </td><td style='text-align: center;'> </td><td style='text-align: right; padding-right:3px;'> </td><td style='text-align: center;'> </td></tr> ";
                string STDPriceTag = "Refer to Std Price List";

                  
 

                //Date Conver for Contract End Date 
               // DateTime tempDateFormat = DateTime.ParseExact(ContractEndDate, "yyyyMMdd", CultureInfo.InvariantCulture);
              //  ContractEndDate = tempDateFormat.ToString("dd MMMM yyyy");

                string tableHeader = "<tr>" +
                    "<td style='width: 3%; border-style: none;padding-left:3px;'><strong>No.</strong></td>" +
                    "<td style='width: 17%; border-style: none;padding-left:3px;'><strong>PRODUCT CODE</strong></td>" +
         "<td style='width: 40%; border-style: none;padding-left:3px;'><strong>PRODUCT DESCRIPTION</strong></td>" +
         "<td style='width: 12%; border-style: none; text-align: right; padding-right:3px;'><strong>Price Ex GST Loose</strong></td> " +
         "<td style='width: 8%; border-style: none;text-align: center;'><strong>UOM</strong></td> " +
         "<td style='width: 12%; border-style: none;text-align: right; padding-right:3px;'><strong>Price Ex GST Pack</strong></td> " +
         "<td style='width: 8%; border-style: none;text-align: center;'><strong>PCK QTY</strong></td></tr>";

                var htmlContent = String.Format("<p><img style='display: block; margin-left: auto; margin-right: auto;' src='https://res.cloudinary.com/gunnersen/image/upload/v1599465601/systemLogos/F1LOGO.jpg' alt='smiley' /></p><h1 style='text-align: center; font-family:Arial;margin-top:-10px'>Contract Price List</h1><h4 style='margin-top:-8px;text-align: center;font-family:Arial;'>" + customerName + "</h4> <div style='text-align:center;font-family:Arial; margin-top: -10px'><span> Effective from:  </span> <span style = 'text-align: center;font-family:Arial; font-weight:bold' > " + reportDate + " </span> <span> </span> <span style = 'text-align: center;font-family:Arial; font-weight:bold' > " + ContractEndDate + " </span></div>  <table style='margin-top:8px;border-color: #99b836; width: 100%;border-style: solid; border-collapse: collapse;font-size: 9px;font-family:Arial;' border='1' cellpadding='4'><tbody>" +
                    tableHeader +
                  "" + rowFormat + "" +
                   "</tbody></table>" +
                   "<p style='font-size:12px;  font-family:Arial;'>No price lists available for the given time period</p>" +
                   "<p style='font-size:9px; font-family:Arial;'><strong>Payment Terms and Conditions:</strong></p>" +
                   "<p style='font-size:small; font-family: Arial;font-size:9px; '>This product quotation and all orders placed with Forest One Australia Pty Ltd are subject to Forest One&rsquo;s Terms and Conditions of Trade which are available on written request or may be viewed at <a href='http://www.forest1.com/terms-and-conditions-3.html'>http://www.forest1.com/terms-and-conditions-3.html</a> ('Forest One Australia Terms'); By accepting this product quotation and/or by placing any order with Forest One Australia Pty Ltd you agree to be bound by Forest One&rsquo;s Terms.</p>" +
                   " ");


                string OutputPath = string.Empty;
                string FileName = string.Empty;
                try
                {
                    var htmlToPdf = new NReco.PdfGenerator.HtmlToPdfConverter();
                    string timestamp = Guid.NewGuid().ToString("N");
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();


                    FileName = "Contract-" + CUNO + " - " + timestamp + ".pdf";
                    OutputPath = @"C:\ForestOnePriceLists\" + FileName;
                    htmlToPdf.GeneratePdf(htmlContent, "", OutputPath);

                    results.Status = true;
                    results.OutputPath = OutputPath;
                    results.FileName = FileName;

                }
                catch (Exception ex)
                {

                    results.Status = false;
                    results.OutputPath = OutputPath + "<<1>>" + ex.Message;
                }


                return results;

            }
            catch (Exception ex)
            {

                results.Status = false;
                results.OutputPath = string.Empty;
                results.Error = "<<2>>" + ex.Message;
                return results;
            }

        }

        private List<PriceItem> GetPriceList(string CUNO, string InDate,  string PRRF, string userid, string password, bool IsNationalCustomer,string PackType)
        {


            List<PriceItem> objPriceList = LstPriceList(CUNO, userid, password, PRRF, IsNationalCustomer);

            string strFormat = "ddMMyyyy";
            DateTime _fdate; 
            if (DateTime.TryParseExact(InDate, strFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _fdate) == true)
            {
                _fdate = _fdate;
            } 

            //Filter from Loose Or Pack
            if (!IsNationalCustomer)
            {
                objPriceList = GetFilteredPriceListFormPackType(PackType, objPriceList, CUNO);
            }

            //Filter from FromDate
            objPriceList = GetFilteredPriceList(_fdate,  objPriceList, CUNO);



            return objPriceList;

        }

        private List<PriceItem> GetFilteredPriceListFormPackType(string packType, List<PriceItem> objPriceList, string cUNO)
        {
            List<PriceItem> newObjPriceList1 = new List<PriceItem>(); 

            foreach (PriceItem item in objPriceList)
            { 
                if (item.PRRF.Trim().Equals(packType))
                {
                    newObjPriceList1.Add(item);
                }
            } 




            return newObjPriceList1;
        }

        private List<PriceItem> GetFilteredPriceList(DateTime fDate,   List<PriceItem> objPriceList, string CUNO)
        {
            List<PriceItem> newObjPriceList1 = new List<PriceItem>();
            List<PriceItem> newObjPriceList2 = new List<PriceItem>();
            List<PriceItem> newObjPriceList3 = new List<PriceItem>();

            foreach (PriceItem item in objPriceList)
            {
                if (fDate >= item.FromDate && fDate <= item.ToDate) {
                    newObjPriceList1.Add(item);
                } 
            }  
            return newObjPriceList1;
        }

        private string GetPRRFforNonNationalContractCustomers(string CUNO, string userid, string password)
        {

            string value = string.Empty;



            value = ConfigurationManager.AppSettings[(GetCustomerFACI(CUNO, userid, password))[0]];

            return value;
        }

        private string GetCustomerFullName(string CUNO, string userid, string password)
        {

            string value = string.Empty;



            value =  (GetCustomerFACI(CUNO, userid, password))[1];

            logger.Info("Level 3 - Calling GetCustomerFullName result: " + value);

            return value;
        }

        private List<string> GetCustomerFACI(string CUNO, string userid, string password)
        {

            List<string> value = new List<string>();
            try
            {
                uint rc;
                string s = getAPIConnectionString();
                rc = getM3ConnectionToCRS610MI(s, userid, password);

                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");

                }

                MvxSock.SetField(ref sid, "CONO", getCONOConfiguration());
                MvxSock.SetField(ref sid, "CUNO", CUNO.Trim());
                rc = MvxSock.Access(ref sid, "GetBasicData");
                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");
                    MvxSock.Close(ref sid);

                }


                value.Add(MvxSock.GetField(ref sid, "CFC3"));
                value.Add(MvxSock.GetField(ref sid, "CUNM"));


                MvxSock.Close(ref sid);
            }
            catch (Exception ex)
            {

                logger.Info("Level 3 - Calling GetCustomerFACI Error: "+ex.StackTrace);

            }
            return value;

        }

        private List<PriceItem> LstPriceList(string CUNO, string userid, string password, string PRRF, bool isNationalCustomer)
        {
            List<PriceItem> objLstPrice = new List<PriceItem>();

            string value = string.Empty;
            try
            {
                uint rc;
                string s = getAPIConnectionString();
                rc = getM3ConnectionToOIS017MI(s, userid, password);





                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");

                }

                rc = MvxSock.Access(ref sid, "SetLstMaxRec");

                MvxSock.SetField(ref sid, "PRRF", PRRF);
                if (!isNationalCustomer)
                {
                    MvxSock.SetField(ref sid, "CUNO", CUNO.Trim());
                }
                MvxSock.SetField(ref sid, "CUCD", "AUD");
            



                rc = MvxSock.Access(ref sid, "LstPriceList");
                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");
                    MvxSock.Close(ref sid);

                }
                CultureInfo culture = new CultureInfo("en-US");
                string strFormat = "yyyyMMdd";
                int counter = 0;
                
                DateTime objDT;
                 


                while (MvxSock.More(ref sid))
                {

                    counter++;

                    PriceItem objPriceItem = new PriceItem();
                    objPriceItem.CONO = MvxSock.GetField(ref sid, "CONO");
                    objPriceItem.CUCD = MvxSock.GetField(ref sid, "CUCD");
                    objPriceItem.CUNO = MvxSock.GetField(ref sid, "CUNO");
                    objPriceItem.FVDT = MvxSock.GetField(ref sid, "FVDT");
                    objPriceItem.LVDT = MvxSock.GetField(ref sid, "LVDT");
                    objPriceItem.PRRF = MvxSock.GetField(ref sid, "PRRF");
                    objPriceItem.SCMO = MvxSock.GetField(ref sid, "SCMO");
                    objPriceItem.TX15 = MvxSock.GetField(ref sid, "TX15");

                    if (DateTime.TryParseExact(objPriceItem.FVDT, strFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out objDT) == true)
                    {
                        objPriceItem.FromDate = objDT;
                    }

                    if (DateTime.TryParseExact(objPriceItem.LVDT, strFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out objDT) == true)
                    {
                        objPriceItem.ToDate = objDT;
                    }


                    MvxSock.Access(ref sid, null);
                    if (!isNationalCustomer)
                    {
                        if (objPriceItem.CUNO.Trim().Equals(CUNO))
                        {
                            objLstPrice.Add(objPriceItem);
                        }
                    }
                    else {

                        if (objPriceItem.PRRF.Trim().Equals(PRRF))
                        {
                            objLstPrice.Add(objPriceItem);
                        }
                    }
                }

                int x = counter;
                MvxSock.Close(ref sid);
            }
            catch (Exception ex)
            {


            }
            return objLstPrice;

        }

        private List<BasePriceItem> LstBasePrice(string PRRF, string CUNO, string CUCD, string FVDT, string userid, string password)
        {
            List<BasePriceItem> objLstBasePriceItem = new List<BasePriceItem>();

            string value = string.Empty;
            try
            {
                uint rc;
                string s = getAPIConnectionString();
                rc = getM3ConnectionToOIS017MI(s, userid, password);

                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");
                }

                rc = MvxSock.Access(ref sid, "SetLstMaxRec");

                MvxSock.SetField(ref sid, "PRRF", PRRF.Trim());
                MvxSock.SetField(ref sid, "CUCD", CUCD.Trim());
                MvxSock.SetField(ref sid, "CUNO", CUNO.Trim());
                MvxSock.SetField(ref sid, "FVDT", FVDT.Trim());

                rc = MvxSock.Access(ref sid, "LstBasePrice");
                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");
                    MvxSock.Close(ref sid);

                }
                CultureInfo culture = new CultureInfo("en-US");
                string strFormat = "yyyyMMdd";
                int counter = 0;
                while (MvxSock.More(ref sid))
                {

                    counter++;

                    BasePriceItem objPriceItem = new BasePriceItem();
                    objPriceItem.CONO = MvxSock.GetField(ref sid, "CONO");
                    objPriceItem.CUCD = MvxSock.GetField(ref sid, "CUCD");
                    objPriceItem.CUNO = MvxSock.GetField(ref sid, "CUNO");
                    objPriceItem.FVDT = MvxSock.GetField(ref sid, "FVDT");
                    objPriceItem.LVDT = MvxSock.GetField(ref sid, "LVDT");
                    objPriceItem.PRRF = MvxSock.GetField(ref sid, "PRRF");
                    objPriceItem.SAPR = MvxSock.GetField(ref sid, "SAPR");
                    double _sapr = Double.Parse(objPriceItem.SAPR);
                    _sapr = Math.Round(_sapr, 2);
                    objPriceItem.SAPR = _sapr.ToString();
                    objPriceItem.TX15 = MvxSock.GetField(ref sid, "TX15");
                    objPriceItem.SACD = MvxSock.GetField(ref sid, "SACD");
                    objPriceItem.SPUN = MvxSock.GetField(ref sid, "SPUN");
                    objPriceItem.MXID = MvxSock.GetField(ref sid, "MXID");
                    if (objPriceItem.MXID.Trim().Length == 0) {
                        objPriceItem.MXID = "0";
                    }

                    objPriceItem.SGGU = MvxSock.GetField(ref sid, "SGGU");
                    objPriceItem.ITNO = MvxSock.GetField(ref sid, "ITNO");
                    objPriceItem.CABA = MvxSock.GetField(ref sid, "CABA");

                    MvxSock.Access(ref sid, null);

                    objLstBasePriceItem.Add(objPriceItem);
                }

                int x = counter;
                MvxSock.Close(ref sid);
            }
            catch (Exception ex)
            {

            }

            //Set ITDS for items
            objLstBasePriceItem = SetITDS(objLstBasePriceItem, userid, password);

            return objLstBasePriceItem;

        }

        private String GetITDSforItem(string ITNO, string userid, string password)
        {
            string value = string.Empty;
            try
            {
                uint rc;
                string s = getAPIConnectionString();
                rc = getM3ConnectionToMMS001MI(s, userid, password);

                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");
                }


                MvxSock.SetField(ref sid, "ITNO", ITNO);

                rc = MvxSock.Access(ref sid, "Get");

                if (rc != 0)
                {
                    MvxSock.ShowLastError(ref sid, "Error no " + rc + "\n");
                    MvxSock.Close(ref sid);

                }
                else
                {

                    value = MvxSock.GetField(ref sid, "FUDS");
                }


                MvxSock.Close(ref sid);
            }
            catch (Exception ex)
            {

            }
            return value;

        }

        private List<BasePriceItem> SetITDS(List<BasePriceItem> objLstBasePriceItem, string userid, string password)
        {
            foreach (BasePriceItem item in objLstBasePriceItem)
            {

                item.ITDS = GetITDSforItem(item.ITNO, userid, password);
            }

            return objLstBasePriceItem;
        }

        #region Download PDF
 
        [Route("ForestOneDataService/ContractGen/GetPDF_OLD")]
        [HttpPost]
        public HttpResponseMessage GetPDF(JObject jsonData)
        { 
            string DocPath = jsonData.Value<string>("DocPath").ToString();
            string FileName = jsonData.Value<string>("FileName").ToString();
             
            string localFilePath; 
            localFilePath = DocPath; 
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            FileStream files = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
            
            response.Content = new StreamContent(files);
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = FileName;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            return response;
        }

        [Route("ForestOneDataService/ContractGen/GETPDF")]
        [HttpGet]
        public HttpResponseMessage Get(String DocPath, String FileName)
        {
            logger.Info( "API Called GET PDF" + Environment.NewLine + DateTime.Now);
            string localFilePath;
            localFilePath = DocPath;
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            FileStream files = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

            response.Content = new StreamContent(files);
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = FileName;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            return response;
        }

        //Generate  the Contract from URL
        [Route("ForestOneDataService/ContractGen/DownloadContract")]
        [HttpGet]
        public HttpResponseMessage GetContractFromURL(String CUNO, String InDate)
        {
            logger.Info("Level1 - Download Contract 1");

            string apiDateFormat = "";
            if (InDate.Length == 10)
            {
                //string inputDateFormat = "yyyy-mm-dd";
                string YEAR = InDate.Substring(0, 4);
                string MONTH = InDate.Substring(5, 2);
                string DATE = InDate.Substring(8, 2);
                apiDateFormat = DATE + MONTH +   YEAR;

            }
            else if(InDate.Length == 8) {

                apiDateFormat = InDate;
            }
            else {

                

                var message = string.Format("Input Date Format Should be 'YYYY-MM-DD' or 'DDMMYYYY'");
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, message);
            }



            Results res = new Results();
            logger.Info("Level1 - Calling GetContract 2");
            res = getContract(CUNO, apiDateFormat,"","");
            logger.Info("Level1 - Complete GetContract 3"); 
            if (res.OutputPath != null)
            {
                logger.Info("Level1 - Output path not null 4");
                string localFilePath;
                localFilePath = res.OutputPath;
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                FileStream files = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

                response.Content = new StreamContent(files);
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = res.FileName;
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

                return response;

            }
            else {

                logger.Info("Level1 - Output path is null 5");
                var message = string.Format("No Contracts Found for "+ CUNO + " given date "+InDate );
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, message);

            }

           
        }



        [Route("ForestOneDataService/ContractGen/BulkContractGeneratre")]
        [HttpPost]
        public HttpResponseMessage BulkContractGeneratre(JObject jsonData)
        {
            logger.Info("Level1 - BulkContractGeneratre 1");
            string CUNO = jsonData.Value<string>("CUNO").ToString();
            string InDate = jsonData.Value<string>("InDate").ToString();
            string StartMonth = jsonData.Value<string>("StartMonth").ToString();
            string WHLO = jsonData.Value<string>("WHLO").ToString();

            

            string apiDateFormat = "";
            if (InDate.Length == 10)
            {
                //string inputDateFormat = "yyyy-mm-dd";
                string YEAR = InDate.Substring(0, 4);
                string MONTH = InDate.Substring(5, 2);
                string DATE = InDate.Substring(8, 2);
                apiDateFormat = DATE + MONTH + YEAR;

            }
            else if (InDate.Length == 8)
            {

                apiDateFormat = InDate;
            }
            else
            {



                var message = string.Format("Input Date Format Should be 'YYYY-MM-DD' or 'DDMMYYYY'");
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, message);
            }



            Results res = new Results();
            logger.Info("Level1 - Calling GetContract 2");
            res = getContract(CUNO, apiDateFormat, StartMonth, WHLO);
            logger.Info("Level1 - Complete GetContract 3");

            if (res.OutputPath != null)
            {
                logger.Info("Level1 - Output path not null 4");
                string localFilePath;
                localFilePath = res.OutputPath;
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                FileStream files = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

                string DocPath = res.OutputPath;
                string fileName = res.FileName;

                String URL = "https://pricelistform.gunnersens.com.au:8070/Service/ForestOneDataService/ContractGen/GETPDF?DocPath="+DocPath+"&FileName=" + fileName;

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(URL) };


            }
            else
            {

                logger.Info("Level1 - Output path is null 5");
                var message = string.Format("No Contracts Found for " + CUNO + " given date " + InDate);
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, message);

            }


        }


            #endregion



            /// <summary>
            /// Gets the API connection string.
            /// </summary>
            /// <returns></returns>
            private static string getAPIConnectionString()
        {

            return ConfigurationManager.AppSettings["M3APIServer"];

        }
        private static string getCONOConfiguration()
        {

            return ConfigurationManager.AppSettings["CONO"];

        }

        /// <summary>Lawson.M3.MvxSoc</summary>
        static SERVER_ID sid = new SERVER_ID();

        private static uint getM3ConnectionToCRS610MI(string host, string user, string password)
        {

            try
            {
                uint rc;
                uint _wait = 1000;
                int port = Convert.ToInt32(ConfigurationManager.AppSettings["M3APIServerPort"]);
                string domainUser = user.Trim() + "@gunnersens";
                uint result1 = MvxSock.SetMaxWait(ref sid, _wait);
                rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "CRS610MI", null);

                /* if (rc == 7) {

                     Thread.Sleep(3000);
                     rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "OIS100MI", null);

                     if (rc == 7)
                     {

                         Thread.Sleep(3000);
                         rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "OIS100MI", null);
                     }
                 }*/

                if (rc != 0)
                {

                    string lasterror = MvxSock.GetLastError(ref sid);
                }


                return rc;
            }
            catch (Exception ex)
            {
                logger.Info("Error IN API Call CRS610 "+ex.StackTrace + Environment.NewLine + DateTime.Now);
                throw;
            }

        }

        private static uint getM3ConnectionToOIS017MI(string host, string user, string password)
        {

            try
            {


                uint rc;
                uint _wait = 1000;
                int port = Convert.ToInt32(ConfigurationManager.AppSettings["M3APIServerPort"]);
                string domainUser = user.Trim() + "@gunnersens";
                uint result1 = MvxSock.SetMaxWait(ref sid, _wait);
                rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "OIS017MI", null);

                /* if (rc == 7) {

                     Thread.Sleep(3000);
                     rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "OIS100MI", null);

                     if (rc == 7)
                     {

                         Thread.Sleep(3000);
                         rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "OIS100MI", null);
                     }
                 }*/

                if (rc != 0)
                {

                    string lasterror = MvxSock.GetLastError(ref sid);
                }


                return rc;
            }
            catch (Exception ex)
            {
                logger.Info("Error IN API Call OIS017 " + ex.StackTrace + Environment.NewLine + DateTime.Now);
                throw;
            }

        }

        private static uint getM3ConnectionToMMS001MI(string host, string user, string password)
        {

            try
            {
                uint rc;
                uint _wait = 1000;
                int port = Convert.ToInt32(ConfigurationManager.AppSettings["M3APIServerPort"]);
                string domainUser = user.Trim() + "@gunnersens";
                uint result1 = MvxSock.SetMaxWait(ref sid, _wait);
                rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "MMS200MI", null);

                /* if (rc == 7) {

                     Thread.Sleep(3000);
                     rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "OIS100MI", null);

                     if (rc == 7)
                     {

                         Thread.Sleep(3000);
                         rc = MvxSock.Connect(ref sid, host, port, domainUser, password, "OIS100MI", null);
                     }
                 }*/

                if (rc != 0)
                {

                    string lasterror = MvxSock.GetLastError(ref sid);
                }


                return rc;
            }
            catch (Exception ex)
            {
                logger.Info("Error IN API Call MMS200" + ex.StackTrace + Environment.NewLine + DateTime.Now);
                throw;
            }

        }

        /// <summary>
        /// Step 1 - If this returns a record we know that we need to go looking for this contract (DXPRRF) in the next step.
        /// </summary>
        /// <param name="CUNO"></param>
        /// <returns></returns>
        private string GetPRRF(string CUNO)
        {

            string PRRF = string.Empty;
            string connectionString = ConfigurationManager.AppSettings.Get("M3DBPRD");

            // Provide the query string with a parameter placeholder.
            string queryString = ConfigurationManager.AppSettings.Get("SQLGetPRRF");

            queryString = queryString.Replace("{{CUNO}}", CUNO);

            // Create and open the connection in a using block. This
            // ensures that all resources will be closed and disposed
            // when the code exits.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Create the Command and Parameter objects.
                SqlCommand command = new SqlCommand(queryString, connection);

                // Open the connection in a try/catch block.
                // Create and execute the DataReader, writing the result
                // set to the console window.

                try
                {


                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {

                        PRRF = reader[0].ToString().Trim();



                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }

            return PRRF;
        }

        private List<Customer> GetCutomerList(string CUNM)
        {

            List<Customer> list = new List<Customer>();
            string connectionString = ConfigurationManager.AppSettings.Get("M3DBPRD");

            // Provide the query string with a parameter placeholder.
            string queryString = ConfigurationManager.AppSettings.Get("SQLGetCUSTOMER");

            queryString = queryString.Replace("{{CUNM}}", CUNM);

            // Create and open the connection in a using block. This
            // ensures that all resources will be closed and disposed
            // when the code exits.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Create the Command and Parameter objects.
                SqlCommand command = new SqlCommand(queryString, connection);

                // Open the connection in a try/catch block.
                // Create and execute the DataReader, writing the result
                // set to the console window.

                try
                {


                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Customer objCustomer = new Customer();
                        objCustomer.CUNO = reader[0].ToString().Trim();
                        objCustomer.CUNM = reader[1].ToString().Trim();

                        list.Add(objCustomer);

                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    logger.Info("Error Get Customer List " + ex.StackTrace + Environment.NewLine + DateTime.Now);
                    Console.WriteLine(ex.Message);
                }

            }

            return list;
        }

        private List<Customer> GetCutomerList(string Param, bool isSearchByCUNO)
        {

            List<Customer> list = new List<Customer>();
            string connectionString = ConfigurationManager.AppSettings.Get("M3DBPRD");

            // Provide the query string with a parameter placeholder.
            string queryString = string.Empty;
            if (isSearchByCUNO)
            {
                queryString = ConfigurationManager.AppSettings.Get("SQLGetCUSTOMERBYNO");


            }
            else
            {

                queryString = ConfigurationManager.AppSettings.Get("SQLGetCUSTOMER");
            }


            queryString = queryString.Replace("{{CUNM}}", Param);

            // Create and open the connection in a using block. This
            // ensures that all resources will be closed and disposed
            // when the code exits.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Create the Command and Parameter objects.
                SqlCommand command = new SqlCommand(queryString, connection);

                // Open the connection in a try/catch block.
                // Create and execute the DataReader, writing the result
                // set to the console window.

                try
                {


                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Customer objCustomer = new Customer();
                        objCustomer.CUNO = reader[0].ToString().Trim();
                        objCustomer.CUNM = reader[1].ToString().Trim();

                        list.Add(objCustomer);

                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }

            return list;
        }


    }
}
