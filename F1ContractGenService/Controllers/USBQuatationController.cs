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

                QT.Owner = "Nilanthi";

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


    }
}
