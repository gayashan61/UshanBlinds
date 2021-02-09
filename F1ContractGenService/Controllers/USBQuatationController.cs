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
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Results;
using F1ContractGenService.BusinessLayer;

namespace F1ContractGenService.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class USBQuatationController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        


        [Route("USBDataSerive/Quatations/TESTAPI")]
        [HttpPost]
        public JsonResult<string> GetData(JObject jsonData)
        {
            string CUNO = string.Empty;
            try
            {
                CUNO = jsonData.Value<string>("CUNO").ToString().ToUpper();

                GenerateQuatation();
                CUNO = "200 OK" + CUNO +" SQL > "+ CheckSQLConnection(CUNO);


            }
            catch (Exception ex)
            {
                
            }
            return Json<string>(CUNO);


        }

        //New Quation
        [Route("USBDataSerive/Quatations/GetNewQuatation")]
        [HttpPost]
        public JsonResult<Quatation> NewQuatation()
        {
            Quatation QT = new Quatation();
            try
            {

                QT.QTCode = new QuationOperations().getNextQTCode();
                QT.Owner = "Ushan Blinds";
                QT.Deposite = "00";

                QuatationLines QTLine = new QuatationLines();
                QTLine.Margin = "10";

                List<QuatationLines> _qtline = new List<QuatationLines>();
                _qtline.Add(QTLine);

                QT.QuatationLines = new List<QuatationLines>(_qtline);

            }
            catch (Exception ex)
            {

            }
            return Json<Quatation>(QT);


        }

        //New Item Price
        [Route("USBDataSerive/Quatations/GetItemPrice")]
        [HttpPost]
        public JsonResult<string> GetItemPrice(JObject jsonData)
        {
            string price = string.Empty;
            try
            {
                BlindRoller br = new BlindRoller();

                br.MAINCAT = jsonData.Value<string>("MAINCAT").ToString().ToUpper();
                br.SUBCAT = jsonData.Value<string>("SUBCAT").ToString().ToUpper();
                br.GROUP = jsonData.Value<string>("GROUP").ToString().ToUpper();
                br.TYPE = jsonData.Value<string>("TYPE").ToString().ToUpper();
                br.WIDTH = jsonData.Value<string>("WIDTH").ToString().ToUpper();
                br.DROP = jsonData.Value<string>("DROP").ToString().ToUpper(); 
                 
                price =  new QuationOperations().GetRollerBlindPrice(br);
                 
            }
            catch (Exception ex)
            {

            }
            return Json<string>(price);


        }

        [Route("USBDataSerive/Quatations/SaveAndGenQT")]
        [HttpPost]
        public string SaveAndGenQT(JObject jsonData)
        {
            string price = string.Empty;
            try
            {
                string HTNLHeader = string.Empty;
                string HTMLLine = string.Empty;
                string Footer = string.Empty;

                JObject header = new JObject();
                JArray lines = new JArray();
                header = jsonData.Value<JObject>("Header");

                HTNLHeader = GenerateQuatationHeader(header);

                lines = jsonData.Value<JArray>("Lines");

                int x = 1;
                foreach (JObject jObj in lines) {

                    string _line = GenerateQuatationLine(jObj,x.ToString());

                    x++;
                    HTMLLine = HTMLLine + _line;
                     
                }
                 
                string FooterFix = "</tbody></table>";


                string SummaryHTML = GenerateQuatationSummary(header);

                string PDFHTNL = HTNLHeader + HTMLLine + FooterFix + SummaryHTML;



                PDFHTNL = GeneratePDF(PDFHTNL);

               

                return PDFHTNL;


            }
            catch (Exception ex)
            {
                return "";
            }  


        }

        [Route("USBDataSerive/Quatations/DownloadPDF")]
        [HttpGet]
        public HttpResponseMessage DownloadPDF(string path)
        {
            string localFilePath;
            localFilePath = @"C:\USBProperties\Documents\Quatations\" + path;
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            FileStream files = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

            response.Content = new StreamContent(files);
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = "QT-20001.pdf";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return response;
        }

        [Route("USBDataSerive/Quatations/SendEmail")]
        [HttpPost]
        public string SendEmail(JObject jsonData)
        {
            string path = jsonData.Value<string>("Path").ToString();
            string To = jsonData.Value<string>("To").ToString();

            new EmailProcessor().SendAlertOnBookingFailed(path, To);
            
            return "OK";
        }

        












            //Generate  the Contract from URL
            [Route("USBDataSerive/Quatations/GetQuatation")]
        [HttpGet]
        public HttpResponseMessage GetQuatation(String CUNO, String InDate)
        {
            logger.Info("Level1 - Download Contract 1");

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
             
             
                logger.Info("Level1 - Output path not null 4");
                string localFilePath;
                localFilePath = @"C:\ForestOneLogs\USB\USB-QT\QT-2000.pdf";
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                FileStream files = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

                response.Content = new StreamContent(files);
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = "QT-2000.pdf";
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

                return response;
             
        }


         



        //Private Functions
        private string CheckSQLConnection(string CUNO)
        {

            string PRRF = string.Empty;
            string connectionString = ConfigurationManager.AppSettings.Get("SSMS_DB");

            // Provide the query string with a parameter placeholder.
            string queryString = "SELECT [username] ,[password], [created_at]  FROM [DB_A6EFF0_gznusb].[dbo].[users]";

            //queryString = queryString.Replace("{{CUNO}}", CUNO);

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

        private bool GenerateQuatation() {

            bool isOk = false;
            string PDFSavePath = ConfigurationManager.AppSettings.Get("PDF_QUAT");
            bool exists = System.IO.Directory.Exists(PDFSavePath); 

            if (!exists)
                System.IO.Directory.CreateDirectory(PDFSavePath);
            string ToDate = DateTime.Now.ToShortDateString();
            //DateTime newDate = DateTime.ParseExact(ToDate, "ddMMyyyy", CultureInfo.InvariantCulture);
            try
            {

                string path = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), @"DocumentTemplates\Quatation\Header.html");
                string[] files = File.ReadAllLines(path);

                 
                string FileName = string.Empty;
                try
                {
                    var htmlToPdf = new NReco.PdfGenerator.HtmlToPdfConverter();
                    string timestamp = Guid.NewGuid().ToString("N");

                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();


                    FileName = "Quatation-" + "" + "-" + "" + "-" + timestamp + ".pdf";
                    PDFSavePath = PDFSavePath + FileName;
                    htmlToPdf.GeneratePdf(files[0], "", PDFSavePath);

                   
                    logger.Info("Level3 - PDF File Generate Completed" + FileName);
                }
                catch (Exception ex)
                {
                    logger.Info("Level3 - PDF File Generate Error Y " + ex.StackTrace);
                   
                }

            }
            catch (Exception ex)
            {  

            }


            return isOk;

        }



        private string GeneratePDF(string PDFString)
        {

           
            string PDFSavePath = ConfigurationManager.AppSettings.Get("PDF_QUAT");
            bool exists = System.IO.Directory.Exists(PDFSavePath);

            if (!exists)
                System.IO.Directory.CreateDirectory(PDFSavePath);
            string ToDate = DateTime.Now.ToShortDateString();
            //DateTime newDate = DateTime.ParseExact(ToDate, "ddMMyyyy", CultureInfo.InvariantCulture);
            try
            { 
                string FileName = string.Empty;
                try
                {
                    var htmlToPdf = new NReco.PdfGenerator.HtmlToPdfConverter();
                    string timestamp = Guid.NewGuid().ToString("N");

                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();


                    FileName = "Quatation-" + timestamp + ".pdf";
                    PDFSavePath = PDFSavePath + FileName;
                    logger.Info("Level3 - PDF File Generate PDFSavePath " + PDFSavePath);
                    logger.Info("Level3 - PDF File Generate PDFString " + PDFString);
                    htmlToPdf.GeneratePdf(PDFString, "", PDFSavePath);


                    logger.Info("Level3 - PDF File Generate Completed" + FileName);
                    return FileName;
                }
                catch (Exception ex)
                {
                    logger.Info("Level3 - PDF File Generate Error Y " + ex.StackTrace);

                }

            }
            catch (Exception ex)
            {

            }


            return PDFSavePath;

        }


        private string GenerateQuatationHeader(JObject header)
        { 
            string HeaderHTML = "";
            string PDFSavePath = ConfigurationManager.AppSettings.Get("PDF_QUAT");
            bool exists = System.IO.Directory.Exists(PDFSavePath);

            if (!exists)
                System.IO.Directory.CreateDirectory(PDFSavePath);
            string ToDate = DateTime.Now.ToShortDateString();
            //DateTime newDate = DateTime.ParseExact(ToDate, "ddMMyyyy", CultureInfo.InvariantCulture);
            try
            { 
                string path = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), @"DocumentTemplates\Quatation\Header.html");
                string[] files = File.ReadAllLines(path);
                 
                foreach (string x in files) {
                    HeaderHTML = HeaderHTML + x;
                }
                HeaderHTML = HeaderHTML.Replace("\n", "").Replace("\r", "");
                  
                HeaderHTML = HeaderHTML.Replace("{{NAME1}}", header.Value<string>("ClientName").ToString());
                HeaderHTML = HeaderHTML.Replace("{{NAME2}}", header.Value<string>("ClientAddress1").ToString());
                HeaderHTML = HeaderHTML.Replace("{{NAME3}}", header.Value<string>("ClientAddress2").ToString() + " "+ header.Value<string>("ClientAddress3").ToString());

                HeaderHTML = HeaderHTML.Replace("{{TEL1}}", header.Value<string>("ClientContact1").ToString());
                HeaderHTML = HeaderHTML.Replace("{{TEL2}}", header.Value<string>("ClientContact2").ToString());
                HeaderHTML = HeaderHTML.Replace("{{TEL3}}", header.Value<string>("ClientContact2").ToString());

                HeaderHTML = HeaderHTML.Replace("{{JOBADR1}}", header.Value<string>("JobAddress1").ToString());
                HeaderHTML = HeaderHTML.Replace("{{JOBADR2}}", header.Value<string>("JobAddress2").ToString());
                HeaderHTML = HeaderHTML.Replace("{{JOBADR3}}", header.Value<string>("JobAddress3").ToString());
                 
            }
            catch (Exception ex)
            { 
            } 
            return HeaderHTML; 
        }

        private string GenerateQuatationLine(JObject header, string No)
        {
            string LineHtml = "";
            string PDFSavePath = ConfigurationManager.AppSettings.Get("PDF_QUAT");
            bool exists = System.IO.Directory.Exists(PDFSavePath);

            if (!exists)
                System.IO.Directory.CreateDirectory(PDFSavePath);
            string ToDate = DateTime.Now.ToShortDateString();
            //DateTime newDate = DateTime.ParseExact(ToDate, "ddMMyyyy", CultureInfo.InvariantCulture);
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), @"DocumentTemplates\Quatation\Lines.html");
                string[] files = File.ReadAllLines(path);

                foreach (string x in files)
                {
                    LineHtml = LineHtml + x;
                }
                LineHtml = LineHtml.Replace("\n", "").Replace("\r", "");

                LineHtml = LineHtml.Replace("{{NO}}", No);
                LineHtml = LineHtml.Replace("{{Code}}", header.Value<string>("Code").ToString());
                LineHtml = LineHtml.Replace("{{Width}}", header.Value<string>("Width").ToString()  );

                LineHtml = LineHtml.Replace("{{Drop}}", header.Value<string>("Drop").ToString());
                LineHtml = LineHtml.Replace("{{Range}}", header.Value<string>("Range").ToString());
                LineHtml = LineHtml.Replace("{{Colour}}", header.Value<string>("Colour").ToString());

                LineHtml = LineHtml.Replace("{{CodeRL}}", header.Value<string>("RL").ToString());
                LineHtml = LineHtml.Replace("{{StackingDetails}}", header.Value<string>("StackingDetails").ToString());
                LineHtml = LineHtml.Replace("{{Note}}", header.Value<string>("Description").ToString());
                LineHtml = LineHtml.Replace("{{Price}}", header.Value<string>("Price").ToString());

            }
            catch (Exception ex)
            {
            }
            return LineHtml;
        }

        private string GenerateQuatationSummary(JObject footer) {

            string HeaderHTML = "";
           
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), @"DocumentTemplates\Quatation\Footer.html");
                string[] files = File.ReadAllLines(path);

                foreach (string x in files)
                {
                    HeaderHTML = HeaderHTML + x;
                }
                HeaderHTML = HeaderHTML.Replace("\n", "").Replace("\r", "");

                HeaderHTML = HeaderHTML.Replace("{{SUBTOT}}", footer.Value<string>("SubTotal").ToString());
                HeaderHTML = HeaderHTML.Replace("{{GST}}", footer.Value<string>("GST").ToString());
                HeaderHTML = HeaderHTML.Replace("{{TOTAL}}", footer.Value<string>("Total").ToString());

                HeaderHTML = HeaderHTML.Replace("{{DEPO}}", footer.Value<string>("Deposite").ToString());
                HeaderHTML = HeaderHTML.Replace("{{BAL}}", footer.Value<string>("Balance").ToString()); 

            }
            catch (Exception ex)
            {
            }
            return HeaderHTML;

        }
    }
}
