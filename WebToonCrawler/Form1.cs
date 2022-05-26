using System.Threading;
using LibWebToonCrawler;
using LibWebToonCrawler.Model;
using LibWebToonCrawler.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WebToonCrawler
{
    public partial class Form1 : Form
    {
        WindowsFormHelper FormHelper = new WindowsFormHelper();

        System.Threading.Thread threadMainJob;

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
            System.Threading.Thread changeStateThread;

            if (RunningFlag)
            {
                changeStateThread = new System.Threading.Thread(new System.Threading.ThreadStart(delegate
                {
                    FormHelper.SetRichTextBox(txtCrawlingInfoJson, "", false);
                    FormHelper.SetLabel(lblSleepRemain, "");
                    FormHelper.buttonToggle(btnRun, "중지", true);
                }));
            }
            else
            {
                changeStateThread = new System.Threading.Thread(new System.Threading.ThreadStart(delegate
                {
                    FormHelper.buttonToggle(btnRun, "중지중...", true);
                    FormHelper.SetLabel(lblSleepRemain, "");
                    FormHelper.SetRichTextBox(txtCrawlingInfoJson, "", true);

                    while (threadMainJob.ThreadState != System.Threading.ThreadState.Stopped)
                    {
                        System.Threading.Thread.Sleep(100);
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
            FormHelper.SetTextBox(txtItemList, "", "N");

            int idx = 0;
            foreach (CrawlingItem item in lstItem)
            {
                idx++;
                FormHelper.SetTextBox(txtItemList
                    , $"{idx.ToString().PadLeft(5, '0')} - {item.ItemTitle} - {item.ItemUrl}\r\n", "Y");
            }
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