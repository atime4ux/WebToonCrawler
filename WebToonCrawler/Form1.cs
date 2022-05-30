using LibWebToonCrawler;
using LibWebToonCrawler.Helper;
using LibWebToonCrawler.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WebToonCrawler
{
    public partial class Form1 : Form
    {
        WindowsFormHelper FormHelper = new WindowsFormHelper();

        Thread threadMainJob;

        bool RunningFlag = false;

        private string LastCrawlingInfoFullPath
        {
            get
            {
                string path = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                string fileName = "LastCrawlingInfo.log";

                return path + "\\" + fileName;
            }
        }


        public string CrawlingInfoJson
        {
            get
            {
                return FormHelper.GetRichTextBoxValue(txtCrawlingInfoJson);
            }
        }


        public Form1()
        {
            InitializeComponent();
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            InitControl();
        }


        private void InitControl()
        {
            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);


            this.lblSleepRemain.Text = "";

            string jsonCrawlingInfo = GetLastCrawlingInfoFile();
            if (string.IsNullOrEmpty(jsonCrawlingInfo) == true)
            {
                jsonCrawlingInfo = JsonConvert.SerializeObject(CrawlerEngine.GetSampleCrawlingInfo(), Formatting.Indented);
            }
            txtCrawlingInfoJson.Text = jsonCrawlingInfo;
        }


        private void ChangeRunningState()
        {
            Thread changeStateThread;

            if (RunningFlag)
            {
                changeStateThread = new Thread(new ThreadStart(delegate
                {
                    FormHelper.SetRichTextBox(txtCrawlingInfoJson, "", false);
                    FormHelper.SetLabel(lblSleepRemain, "");
                    FormHelper.buttonToggle(btnRun, "중지", true);
                }));
            }
            else
            {
                changeStateThread = new Thread(new ThreadStart(delegate
                {
                    FormHelper.buttonToggle(btnRun, "중지중...", true);
                    FormHelper.SetLabel(lblSleepRemain, "");
                    FormHelper.SetRichTextBox(txtCrawlingInfoJson, "", true);

                    while (threadMainJob.ThreadState != ThreadState.Stopped)
                    {
                        Thread.Sleep(100);
                    }

                    FormHelper.buttonToggle(btnRun, "실행", true);
                }));
            }

            changeStateThread.Start();
        }


        private void WriteStatus(string status)
        {
            if (txtLog.Text.Length > 8000)
            {
                FormHelper.SetTextBox(txtLog, "", "N");
            }

            FormHelper.SetTextBox(txtLog, status + "\r\n", "Y");
        }

        private void WriteItem(List<CrawlingItem> lstItem)
        {
            var itemStatus = lstItem.GroupBy(x => new { x.ItemTitle, x.ItemNumber }).OrderBy(x => x.Key.ItemTitle).ThenBy(x => x.Key.ItemNumber).Select(x =>
            {
                return $"{x.Key.ItemTitle} - {x.Key.ItemNumber} : {x.Count(g => g.DownloadComplete == true)}/{x.Count()}";
            }).ToArray();

            FormHelper.SetTextBox(txtItemList, string.Join("\r\n", itemStatus), "N");
        }

        private void WriteSleepStatus(string sleepStatus)
        {
            FormHelper.SetLabel(lblSleepRemain, sleepStatus);
        }


        private void SaveMonitoringInfoFile()
        {
            CommonHelper.OverwriteFile(LastCrawlingInfoFullPath, txtCrawlingInfoJson.Text);
        }


        private string GetLastCrawlingInfoFile()
        {
            return CommonHelper.ReadFile(LastCrawlingInfoFullPath);
        }

        private void StartAndStop()
        {
            if (!RunningFlag)
            {
                //start
                SaveMonitoringInfoFile();

                CrawlerEngine crawlerEngine = new CrawlerEngine(
                    CrawlingInfoJson,
                    new Logger(WriteStatus, WriteItem, WriteSleepStatus)
                    );

                RunningFlag = true;
                
                threadMainJob = new Thread(new ThreadStart(crawlerEngine.Run));
                threadMainJob.Name = "loopStart";
                threadMainJob.Start();
            }
            else
            {
                //stop
                RunningFlag = false;
            }

            ChangeRunningState();
        }


        private void btnRun_Click(object sender, EventArgs e)
        {
            StartAndStop();
        }


        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            if (threadMainJob != null)
            {
                threadMainJob.Abort();
            }
        }
    }
}