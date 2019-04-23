using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Zakupki
{
    public partial class Form1 : Form
    {
        private DateTime DateFrom { get; set; }
        private DateTime DateTo { get; set; }
        private string FolderName { get; set; }
        private HashSet<string> Errors { get; set; }

        public Form1()
        {
            InitializeComponent();
            dateTimePicker1.Format = DateTimePickerFormat.Short;
            dateTimePicker2.Format = DateTimePickerFormat.Short;
            dateTimePicker1.Value = DateTime.Now.AddDays(-4);
            dateTimePicker2.Value = DateTime.Now.AddDays(-1);
            dateTimePicker2.MaxDate = DateTime.Now.AddDays(-1);
            label5.Text = "Статус: не начато";
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            DateFrom = dateTimePicker1.Value.Date;
        }

        private void dateTimePicker2_ValueChanged(object sender, EventArgs e)
        {
            DateTo = dateTimePicker2.Value.AddDays(1).Date.AddMilliseconds(-1);
        }

        private void label2_Click(object sender, EventArgs e)
        {
        }

        private void label3_Click(object sender, EventArgs e)
        {
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
        }

        private void label5_Click(object sender, EventArgs e)
        {
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                FolderName = folderBrowserDialog1.SelectedPath;
                textBox1.Text = FolderName;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            label5.Text = "Статус: Загрузка";
            GetOrders();
            label5.Text = "Статус: Окончено";
        }

        private void GetOrders()
        {
            List<string> dates = new List<string>();

            foreach (DateTime day in EachDay(DateFrom, DateTo))
            {
                dates.Add(day.ToString("yyyyMMdd"));
            }

            //223 fz

            string url223 = "ftp://ftp.zakupki.gov.ru/out/published/";
            string password223 = "fz223free";

            FtpWebRequest request223 = CreateRequest(url223,
                WebRequestMethods.Ftp.ListDirectory, password223);

            List<string> listRegion223 = GetResponseList(request223);


            foreach (string region in listRegion223)
            {
                try
                {
                    string url = url223 + region + "/purchaseNotice/daily/";

                    if (!(region.Equals("archive") || region.EndsWith(".txt") ||
                          region.EndsWith(".sh") || region.Equals("undefined")))
                    {
                        GetFilesFromRegion(url, password223, dates);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("по 223 фз ошибка: " + e);
                }
            }


            //94 fz

            string url94 = "ftp://ftp.zakupki.gov.ru/fcs_regions/";
            string password94 = "free";

            FtpWebRequest request94 = CreateRequest(url94,
                WebRequestMethods.Ftp.ListDirectory, password94);

            List<string> listRegion94 = GetResponseList(request94);

            foreach (var region in listRegion94)
                try
                {
                    string url = url94 + region + "/notifications/currMonth/";

                    if (!(region.Equals("_logs") || region.Equals("PG-PZ") || region.EndsWith(".zip") ||
                          region.Equals("control99docs") || region.Equals("fcs_undefined") || region.EndsWith(".sh")
                          || region.EndsWith(".txt") || region.EndsWith(".log")))
                    {
                        GetFilesFromRegion(url, password94, dates);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("по 94 фз ошибка: " + e);
                }
        }

        private void GetFilesFromRegion(string url, string password, List<string> dates)
        {
            FtpWebRequest requestRegion =
                CreateRequest(url, WebRequestMethods.Ftp.ListDirectory, password);

            var allFiles = GetResponseList(requestRegion);

            HashSet<string> files = new HashSet<string>();

            Errors = new HashSet<string>();


            foreach (string file in allFiles)
            {
                foreach (var date in dates)
                {
                    if (file.Contains(date))
                    {
                        files.Add(file);
                    }
                }
            }

            foreach (var file in files)
            {
                try
                {
                    Download(url + file, file, FolderName, password);
                }
                catch (Exception e)
                {
                    Errors.Add(file);
                }
            }

            if (Errors.Count > 0)
            {
                foreach (var file in Errors)
                {
                    try
                    {
                        Download(url + file, file, FolderName, password);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Файл " + url + file + " не  удалось скачать с ftp сервера");
                    }
                }
            }
        }


        private FtpWebRequest CreateRequest(string uri, string method, string credentials)
        {
            FtpWebRequest request = (FtpWebRequest) WebRequest.Create(uri);
            request.Method = method;
            request.Credentials = new NetworkCredential(credentials, credentials);

            return request;
        }


        private List<string> GetResponseList(FtpWebRequest request)
        {
            var list = new List<string>();

            using (var response = (FtpWebResponse) request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream, true))
                    {
                        while (!reader.EndOfStream)
                        {
                            list.Add(reader.ReadLine());
                        }
                    }
                }
            }

            return list;
        }


        private IEnumerable<DateTime> EachDay(DateTime from, DateTime to)
        {
            for (var day = from.Date; day.Date <= to.Date; day = day.AddDays(1))
                yield return day;
        }
        

        public void Download(string remotePath, string fileNameToDownload, string saveToLocalPath, string password)
        {
            try
            {
                FtpWebRequest request = CreateRequest(remotePath, WebRequestMethods.Ftp.DownloadFile, password);
                request.UseBinary = true;
                request.KeepAlive = false;

                FtpWebResponse response = request.GetResponse() as FtpWebResponse;
                Stream responseStream = response.GetResponseStream();
                FileStream outputStream = new FileStream(saveToLocalPath + "\\" + fileNameToDownload, FileMode.Create);

                int bufferSize = 1024;
                int readCount;
                byte[] buffer = new byte[bufferSize];

                readCount = responseStream.Read(buffer, 0, bufferSize);
                while (readCount > 0)
                {
                    outputStream.Write(buffer, 0, readCount);
                    readCount = responseStream.Read(buffer, 0, bufferSize);
                }

                responseStream.Close();
                outputStream.Close();
                response.Close();
            }
            catch (Exception e)
            {
                Errors.Add(remotePath);
                MessageBox.Show("Файл " + remotePath + " не скачался с ftp сервера");
            }
        }
    }
}