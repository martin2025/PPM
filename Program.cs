using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Data;
using System.IO;
using System.Net;
using System.Text;

namespace Parse_Private_Messages
{
    internal class Program
    {
        public static decimal dversion = 1.1m;
        public static DateTime dbitcointalkdate;
        public static string sfile = "";
        public static string sprofile = "";
        public static string spassword = "";
        public static string scode = "";
        public static string ssec = "";
        public static string stimer = "";
        public static string surl = "";
        public static string ssections = "";
        public static string ssection = "";
        public static string soutput = "";
        public static string sformat = "";
        public static int ierr = 0;
        public static int idelaytimer = 0;
        public static HtmlDocument htmlDoc = new HtmlDocument();
        public static MyWebClient wclient = new MyWebClient();
        public static DateTime ParseStartTime;
        public static DateTime ParseEndTime;
        public static int inumpage = 0;
        public static int icurpage = 0;
        public static DataTable tablePM = new DataTable();
        public static DataRow drrow;

        public class Option
        {
            public string profile { get; set; }
            public string password { get; set; }
            public string bypasscode { get; set; }
            public int delaytime { get; set; }
            public string pmsection { get; set; }
            public string outfileloc { get; set; }
            public string outformat { get; set; }
        }

        private static void Main(string[] args)
        {
            {
                UpDateLog("Reading option.json", 1);
                try
                {
                    dynamic result = JsonConvert.DeserializeObject<Option>(File.ReadAllText(@"option.json"));
                    sprofile = result.profile;
                    spassword = result.password;
                    scode = result.bypasscode;
                    idelaytimer = 1000 + result.delaytime;
                    ssec = result.pmsection;
                    soutput = result.outfileloc;
                    sformat = result.outformat;

                    if (ssec == "inbox")
                    {
                        ssections = "inbox";
                    }
                    else if (ssec == "outboth")
                    {
                        ssections = "outbox";
                    }
                    else if (ssec == "both")
                    {
                        ssections = "inbox,outbox";
                    }
                    else
                    {
                        throw new Exception("option pmsection error.  see readme");
                    }

                    //TODO:  Validate output location, create temp log file.

                }
                catch (Exception)
                {
                    UpDateLog("option json error.  see readme");
                    throw;
                }

                UpDateLog(" Version " + dversion + " - Delay " + idelaytimer.ToString());

                tablePM.Columns.Add("Section", typeof(string));
                tablePM.Columns.Add("DateTime", typeof(DateTime));
                tablePM.Columns.Add("Subject", typeof(string));
                tablePM.Columns.Add("From", typeof(string));
                tablePM.Columns.Add("To", typeof(string));
                tablePM.Columns.Add("PM", typeof(string));
                tablePM.Columns.Add("Responded", typeof(string));
                tablePM.Columns.Add("Page", typeof(int));
            }

            foreach (string ssection in ssections.Split(","))
            {
                Forum_Signin();
                UpDateLog("Parsing " + ssection, 1);
                surl = "https://bitcointalk.org/index.php?action=pm";
                if (ssection == "outbox")
                {
                    surl += ";f=outbox;";
                }
                else if (ssection == "inbox")
                {
                    surl += ";f=intbox;";
                }

                surl += "sort=date;desc;";
                htmlDoc = wclient.GetPage(surl + "start=0;");
                try
                {
                    dbitcointalkdate = Convert.ToDateTime(htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[1]/table[2]/tr[1]/td[2]/span").InnerText);
                }
                catch (Exception)
                {
                    UpDateLog("was not able to login to forum");
                    throw;
                }

                string sinumpages;
                if (htmlDoc.DocumentNode.SelectSingleNode("//form[@name='pmFolder']/div[1]/table[1]/tr/td/div[1]") != null)
                {
                    if (htmlDoc.DocumentNode.SelectSingleNode("//form[@name='pmFolder']/div[1]/table[1]/tr/td/div[1]/a[last()]") != null)
                    {
                        sinumpages = htmlDoc.DocumentNode.SelectSingleNode("//form[@name='pmFolder']/div[1]/table[1]/tr/td/div[1]/a[last()]").InnerText;
                    }
                    else
                    {
                        sinumpages = "1";
                    }

                    UpDateLog("Pages of PMs: " + sinumpages);

                    int.TryParse(sinumpages, out inumpage);
                    icurpage = 0;
                    while (icurpage < inumpage)
                    {
                        ParseStartTime = DateTime.UtcNow;
                        surl = "https://bitcointalk.org/index.php?action=pm";
                        if (ssection == "outbox")
                        {
                            surl += ";f=outbox;";
                        }
                        else if (ssection == "inbox")
                        {
                            surl += ";f=intbox;";
                        }

                        surl += ";sort=date;desc;";
                        if (icurpage > 0)
                        {
                            surl += "start=" + (icurpage * 20).ToString();
                            //TODO:  reload the page if error
                            htmlDoc = wclient.GetPage(surl);
                        }

                        if (htmlDoc.DocumentNode.SelectNodes("//form[@name='pmFolder']/table[3]") != null)
                        {
                            // now get each post for each page
                            foreach (HtmlNode node in htmlDoc.DocumentNode.SelectNodes("//form[@name='pmFolder']/table[3]/tr"))
                            {
                                if (node.SelectSingleNode("td/table") != null)
                                {
                                    drrow = tablePM.NewRow();
                                    drrow["Section"] = ssection;
                                    string swork = node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[2]/table/tr/td[1]").InnerText;
                                    if (GetBetween(swork, "on:", " &#187;").Contains("Today"))
                                    {
                                        swork = swork.Replace("Today at", dbitcointalkdate.ToShortDateString());
                                    }
                                    drrow["DateTime"] = Convert.ToDateTime(GetBetween(swork, "on:", " &#187;"));
                                    drrow["Subject"] = WebUtility.HtmlEncode(GetBetween(swork, "", "&#171;")).Trim();
                                    if (node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[1]/b/a") != null)
                                    {
                                        drrow["From"] = WebUtility.HtmlEncode(node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[1]/b/a").InnerHtml);
                                    }
                                    else
                                    {
                                        drrow["From"] = "Bitcoin Forum Guest";
                                    }

                                    drrow["To"] = WebUtility.HtmlEncode(GetBetween(swork, "to: ", " on")).Trim();
                                    if (node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[2]/div") != null)
                                    {
                                        drrow["PM"] = WebUtility.HtmlEncode(node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[2]/div").InnerHtml);
                                    }
                                    else
                                    {
                                        drrow["PM"] = "Error";
                                    }

                                    if (swork.Contains("&#171; You have forwarded or responded to this message. &#187;"))
                                    {
                                        drrow["Responded"] = "Yes";
                                    }
                                    else
                                    {
                                        drrow["Responded"] = "No";
                                    }

                                    drrow["Page"] = (icurpage + 1);
                                    tablePM.Rows.Add(drrow);
                                }
                            }
                        }
                        else
                        {
                            UpDateLog("No messages found", 1);
                            break;
                        }

                        if (htmlDoc != null)
                        {
                            foreach (HtmlNode node in htmlDoc.DocumentNode.SelectNodes("//form[@name='pmFolder']/table[3]/tr"))
                            {
                                if (node.SelectSingleNode("td/table") != null)
                                {
                                    drrow = tablePM.NewRow();
                                    drrow["Section"] = ssection;
                                    string swork = node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[2]/table/tr/td[1]").InnerText;
                                    if (GetBetween(swork, "on:", " &#187;").Contains("Today"))
                                    {
                                        swork = swork.Replace("Today at", dbitcointalkdate.ToShortDateString());
                                    }
                                    drrow["DateTime"] = Convert.ToDateTime(GetBetween(swork, "on:", " &#187;"));
                                    drrow["Subject"] = WebUtility.HtmlEncode(GetBetween(swork, "", "&#171;")).Trim();
                                    if (node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[1]/b/a") != null)
                                    {
                                        drrow["From"] = WebUtility.HtmlEncode(node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[1]/b/a").InnerHtml);
                                    }
                                    else
                                    {
                                        drrow["From"] = "Bitcoin Forum Guest";
                                    }

                                    drrow["To"] = WebUtility.HtmlEncode(GetBetween(swork, "to: ", " on")).Trim();
                                    if (node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[2]/div") != null)
                                    {
                                        drrow["PM"] = WebUtility.HtmlEncode(node.SelectSingleNode("td/table/tr/td/table/tr[1]/td[2]/div").InnerHtml);
                                    }
                                    else
                                    {
                                        drrow["PM"] = "Error";
                                    }

                                    if (swork.Contains("&#171; You have forwarded or responded to this message. &#187;"))
                                    {
                                        drrow["Responded"] = "Yes";
                                    }
                                    else
                                    {
                                        drrow["Responded"] = "No";
                                    }

                                    drrow["Page"] = (icurpage + 1);
                                    tablePM.Rows.Add(drrow);
                                }
                            }
                        }

                        icurpage += 1;
                        UpDateLog("Parsed page: " + icurpage.ToString() + " / " + sinumpages);
                        ParseEndTime = ParseStartTime.AddMilliseconds(idelaytimer);
                        while (DateTime.UtcNow <= ParseEndTime)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                }
            }

            // saving file
            if (tablePM.Rows.Count > 0)
            {
                string docPath = soutput + "\\" + sprofile + "-results-" + DateTime.Now.ToString("MMMM-yyyy") + "." + sformat;
                UpDateLog("Saving '" + docPath);
                if (sformat == "xml")
                {
                    tablePM = tablePM.DefaultView.ToTable();
                    System.IO.StringWriter writer = new System.IO.StringWriter();
                    tablePM.TableName = "tablePM";
                    tablePM.WriteXml(writer, XmlWriteMode.WriteSchema, false);
                    File.WriteAllText(docPath, writer.ToString());
                    UpDateLog("Saved.  Program Complete.   clubcrypto.live");
                }
                else if (sformat == "json")
                {
                    File.WriteAllText(docPath, JsonConvert.SerializeObject(tablePM));
                    UpDateLog("Saved.  Program Complete.   clubcrypto.live");
                }
            }
            else
            {
                UpDateLog("Nothing to save", 1);
            }
        }

        // program end

        private static void Forum_Signin()
        {
            UpDateLog("Attempting to Login as " + sprofile + ": ", 1);
            string uri = "https://bitcointalk.org/index.php?action=login2;ccode=" + scode + "&user=" + sprofile + "&passwrd=" + spassword;
            htmlDoc = wclient.GetPage(uri);
            UpDateLog(" done!", 1);
        }
        public static HtmlDocument GetPage(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            HtmlDocument doc = new HtmlDocument();

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream stream = response.GetResponseStream();
                System.Threading.Thread.Sleep(100);
                using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding("iso-8859-1")))
                {
                    string html = reader.ReadToEnd();
                    doc.LoadHtml(html);
                    return doc;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                System.Threading.Thread.Sleep(5000);
                doc = null;
                return doc;
            }
        }
        public static string GetBetween(string pageHTML, string searchfor1, string searchfor2)
        {
            int iSpos, iEpos;
            if (searchfor1 == "")
            { iSpos = 0; }
            else
            { iSpos = pageHTML.IndexOf(searchfor1, 0) + searchfor1.Length; }

            if (searchfor2 == "")
            { iEpos = pageHTML.Length; }
            else
            { iEpos = pageHTML.IndexOf(searchfor2, iSpos); }

            if (iEpos - iSpos < 0)
            {
                return "";
            }
            else
            {
                return pageHTML.Substring(iSpos, iEpos - iSpos);
            }

        }
        public static void UpDateLog(string sLog = "", int ilogclass = 1)
        {
            if (sLog.Length > 1000)
            {
                sLog = sLog.Substring(0, 1000);
            }
            if (ilogclass == 1)
            {
                Console.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " - " + sLog);
            }
            else
            {
                Console.Write(DateTime.UtcNow.ToString("HH:mm:ss") + " - " + sLog);
            }
        }
        public class MyWebClient
        {
            private CookieContainer _cookies = new CookieContainer();
            public void ClearCookies()
            {
                _cookies = new CookieContainer();
            }
            public HtmlDocument GetPage(string url)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.CookieContainer = _cookies;
                HttpWebResponse response;
                HtmlDocument doc = new HtmlDocument();

                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    Stream stream = response.GetResponseStream();
                    using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding("iso-8859-1")))
                    {
                        string html = reader.ReadToEnd();
                        doc.LoadHtml(html);
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("The remote server returned an error: (503)"))
                    {
                        Console.WriteLine();
                        UpDateLog("*BITCOINTALK VISIT TOO FAST* Wait 30 seconds...");
                        Console.WriteLine();
                        ParseEndTime = DateTime.UtcNow.AddSeconds(30);
                        System.TimeSpan dtdiff;
                        while (ParseEndTime > DateTime.UtcNow)
                        {
                            dtdiff = ParseEndTime.Subtract(DateTime.UtcNow);
                            Console.WriteLine(dtdiff.ToString());
                            System.Threading.Thread.Sleep(3000);
                        }
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("--" + ex.Message);
                        Console.WriteLine();
                        System.Threading.Thread.Sleep(5000);
                        throw;
                    }
                }
                return doc;
            }
        }
    }
}
