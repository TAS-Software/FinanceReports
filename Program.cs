using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FinanceReports
{
    class Program
    {
        static void Main(string[] args)
        {
            var checkDate = DateTime.Now.Date;

            if (checkDate.Day == 1)
            {
                RunQuaratineBatchesReport();
                RunStockValuationReport();
                SendMail("Finance Reports", "Both Ran. " + checkDate.Day);
            }
            else
            {
                RunQuaratineBatchesReport();
                SendMail("Finance Reports", "Only Quarantine Ran Today. " + checkDate.Day);
            }
        }

        private static void RunQuaratineBatchesReport()
        {
            if (!IsDuringRestore(DateTime.Now))
            {
                try
                {
                    using (var reportDB = new report01thas01Entities())
                    {
                        string regexPattern = @"\{\*?\\[^{}]+}|[{}]|\\\n?[A-Za-z]+\n?(?:-?\d+)?[ ]?";
                        Regex rgx = new Regex(regexPattern);

                        reportDB.Database.CommandTimeout = 10000;
                        Console.WriteLine("Begin Retreiving Quarantine Batches Dataset...");
                        var financeQuaratineDataset = reportDB.THAS_CONNECT_FinanceQuarantineBatches().ToList();

                        Console.WriteLine("Successfully Retreived Dataset");
                        Console.WriteLine("Awaiting Excel Generation...");

                        FileInfo fileInfo;
                        string theDate = DateTime.Now.ToString("yyyyMMdd");
                        string theDateHours = DateTime.Now.ToString("yyyyMMdd HH.mm.ss");
                        if (CreateDirectoryStructure(out fileInfo, theDate, theDateHours, @"FinanceQuarantineBatches", "Finance Reports", true)) //Finance Reports
                        {
                            using (ExcelPackage excelPackage = new ExcelPackage(fileInfo))
                            {
                                var workSheet = excelPackage.Workbook.Worksheets.Add("QuarantineBatches");
                                workSheet.Cells["A1"].LoadFromCollection(financeQuaratineDataset, true, OfficeOpenXml.Table.TableStyles.Medium2);
                                int rowCount = workSheet.Dimension.Rows;

                                workSheet.Cells[workSheet.Dimension.Address].AutoFitColumns();
                                workSheet.View.ZoomScale = 75;
                                excelPackage.Save();
                                Console.WriteLine("Successfully Generated Finance Quarantine Batches Costings Excel File");
                                SendMail("Quarantine Batches Report", "Run Successfully");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Quaratine Finance Report Has Failed. Reason: " + ex.Message + ex.InnerException.Message + ex.InnerException);
                }
            }
        }

        private static void RunStockValuationReport()
        {
            if (!IsDuringRestore(DateTime.Now))
            {
                try
                {
                    using (var reportDB = new report01thas01Entities())
                    {

                        string regexPattern = @"\{\*?\\[^{}]+}|[{}]|\\\n?[A-Za-z]+\n?(?:-?\d+)?[ ]?";
                        Regex rgx = new Regex(regexPattern);

                        reportDB.Database.CommandTimeout = 10000;
                        Console.WriteLine("Begin Retreiving Stock Valuation Dataset...");
                        var stockValDataset = reportDB.THAS_CONNECT_StockValuationReport().ToList();

                        foreach (var stockVal in stockValDataset)
                        {

                            stockVal.Notes = stockVal.Notes != null ? rgx.Replace(stockVal.Notes, "") : stockVal.Notes;

                            if (stockVal.Exclude_From_Provision == "N")
                            {
                                if (stockVal.Provision____TAS_method_ == "100%")
                                {
                                    stockVal.Provision_Cost__TAS_method_ = stockVal.Material_Cost__.HasValue ? stockVal.Material_Cost__.Value : 0.0m;
                                }
                                else
                                {
                                    stockVal.Provision_Cost__TAS_method_ = 0.0m;
                                }
                            }
                            stockVal.Adjust_Value__TAS_method_ = stockVal.Material_Cost__.HasValue ? stockVal.Material_Cost__.Value : 0.0m - stockVal.Provision_Cost__TAS_method_;

                            if (stockVal.Method_Type.ToLower() == "purchased")
                            {
                                stockVal.Type_Of_Stock = "Raw Materials";

                            }
                            else if (stockVal.Seat_ == "YES" || stockVal.Product_Group_Code.ToLower() == "frm/frf")
                            {
                                stockVal.Type_Of_Stock = "Finished Goods";
                            }
                            else
                            {
                                stockVal.Type_Of_Stock = "Manufactured Parts";
                            }
                        }

                        Console.WriteLine("Successfully Retreived Dataset");
                        Console.WriteLine("Awaiting Excel Generation...");

                        FileInfo fileInfo;
                        string theDate = DateTime.Now.ToString("yyyyMMdd");
                        string theDateHours = DateTime.Now.ToString("yyyyMMdd HH.mm.ss");
                        if (CreateDirectoryStructure(out fileInfo, theDate, theDateHours, @"StockValuation12AM", "Finance Reports", true)) //Finance Reports
                        {
                            using (ExcelPackage excelPackage = new ExcelPackage(fileInfo))
                            {
                                var workSheet = excelPackage.Workbook.Worksheets.Add("StockValuation");
                                workSheet.Cells["A1"].LoadFromCollection(stockValDataset, true, OfficeOpenXml.Table.TableStyles.Medium2);
                                int rowCount = workSheet.Dimension.Rows;
                                workSheet.Cells["I2:I" + rowCount].Style.Numberformat.Format = "dd/MM/yyyy";
                                workSheet.Cells["J2:J" + rowCount].Style.Numberformat.Format = "dd/MM/yyyy";
                                workSheet.Cells["K2:K" + rowCount].Style.Numberformat.Format = "dd/MM/yyyy";
                                workSheet.Cells["L2:L" + rowCount].Style.Numberformat.Format = "dd/MM/yyyy";
                                workSheet.Cells["U2:U" + rowCount].Style.Numberformat.Format = "dd/MM/yyyy";
                                workSheet.Cells["Z2:Z" + rowCount].Style.Numberformat.Format = "dd/MM/yyyy";

                                workSheet.Cells[workSheet.Dimension.Address].AutoFitColumns();
                                workSheet.View.ZoomScale = 75;
                                excelPackage.Save();
                                Console.WriteLine("Successfully Generated Stock Valuation Costings Excel File");
                                SendMail("Stock Valuation Report", "Run Successfully");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Stock Valuation Costings Report Has Failed. Reason: " + ex.Message + ex.InnerException.Message + ex.InnerException);
                }
            }
        }

        private static void SendMail(string message, string result)
        {
            try
            {
                string from = "FinanceReports@thompsonaero.com";
                string to = "sean.kelly@thompsonaero.com";

                using (MailMessage mail = new MailMessage(from, to))
                {
                    mail.Subject = message;
                    mail.Body = result;
                    mail.IsBodyHtml = true;
                    SmtpClient client = new SmtpClient("remote.thompsonaero.com");
                    client.Send(mail);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.InnerException);
            }
        }
        private static bool CreateDirectoryStructure(out FileInfo fileInfo, string date, string dateHours, string filename, string folderPath, bool costed)
        {
            string path = @"\\tas\reports$\{0}\{1}\";
            if (costed)
            {
                path = @"\\tas\reports$\{0}\With Costing Info\{1}\";
                //path = @"\\tas\reports$\Test\With Costing Info\{1}\";
            }
            else
            {
                path = @"\\tas\reports$\{0}\Without Costing Info\{1}\";
                //path = @"\\tas\reports$\Test\Without Costing Info\{1}\";
            }

            fileInfo = new FileInfo(string.Format(path + filename + "_{2}.xlsx", folderPath, date, dateHours));
            try
            {
                var fullpath = string.Format(path + filename + "_{2}.xlsx", folderPath, date, dateHours);
                if (!File.Exists(fullpath))
                {
                    fileInfo = new FileInfo(fullpath);
                    fileInfo.Directory.Create();
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Issue : " + ex.Message);
                return false;
            }
        }

        private static bool IsDuringRestore(DateTime timeNow) // Sleep during restore time.
        {
            if (timeNow.TimeOfDay.Minutes < 14)
            {
                var diff = (15 - timeNow.TimeOfDay.Minutes) * 60000;
                Console.WriteLine("Sleeping For " + (diff / 60000) + " Mins");
                System.Threading.Thread.Sleep(diff);
                return false;
            }
            while (!IsServerConnected())
            {
                Console.WriteLine("Sleeping For A Minute Here...");
                System.Threading.Thread.Sleep(60000);
            }
            Console.WriteLine("Not In Restore Window - Good To Go " + timeNow);
            return false;
        }
        public static bool IsServerConnected()
        {
            using (var l_oConnection = new SqlConnection(@"data source=THAS-REPORT01\THOMPSONSQL;initial catalog=thas01;persist security info=True;Integrated Security=SSPI;"))
            {
                try
                {
                    l_oConnection.Open();
                    Console.WriteLine("Open");
                    return true;

                }
                catch (SqlException)
                {
                    Console.WriteLine("Closed");
                    return false;
                }
            }
        }
    }
}
