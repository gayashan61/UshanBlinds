using F1ContractGenService.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace F1ContractGenService.BusinessLayer
{
    public class QuationOperations
    {
        public string GetRollerBlindPrice(BlindRoller br)
        {

            string PRRF = string.Empty;
            string connectionString = ConfigurationManager.AppSettings.Get("SSMS_DB");

            // Provide the query string with a parameter placeholder.
            string SQL_Blinds_Roller = ConfigurationManager.AppSettings.Get("Blinds_Roller");
           
            SQL_Blinds_Roller = SQL_Blinds_Roller.Replace("{{MAINCAT}}", br.MAINCAT);
            SQL_Blinds_Roller = SQL_Blinds_Roller.Replace("{{SUBCAT}}", br.SUBCAT);
            SQL_Blinds_Roller = SQL_Blinds_Roller.Replace("{{GROUP}}", br.GROUP);
            SQL_Blinds_Roller = SQL_Blinds_Roller.Replace("{{TYPE}}", br.TYPE);
            SQL_Blinds_Roller = SQL_Blinds_Roller.Replace("{{WIDTH}}", br.WIDTH);
            SQL_Blinds_Roller = SQL_Blinds_Roller.Replace("{{DROP}}", br.DROP); 

            // Create and open the connection in a using block. This
            // ensures that all resources will be closed and disposed
            // when the code exits.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Create the Command and Parameter objects.
                SqlCommand command = new SqlCommand(SQL_Blinds_Roller, connection);

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

        public string getNextQTCode() {

            
            string PRRF = string.Empty;
            string connectionString = ConfigurationManager.AppSettings.Get("SSMS_DB");

            // Provide the query string with a parameter placeholder.
            string SQL_Blinds_Roller = ConfigurationManager.AppSettings.Get("NextQTCode");

           

            // Create and open the connection in a using block. This
            // ensures that all resources will be closed and disposed
            // when the code exits.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Create the Command and Parameter objects.
                SqlCommand command = new SqlCommand(SQL_Blinds_Roller, connection);

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

        public string ProcessQuatation(JObject Header, JArray Lines) {

            string Path = string.Empty;




            return Path;
        }
    
    }
}