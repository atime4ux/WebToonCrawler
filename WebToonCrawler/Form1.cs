using LibWebToonCrawler;
using LibWebToonCrawler.Helper;
using LibWebToonCrawler.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebToonCrawler
{
    public partial class Form1 : Form
    {
        WindowsFormHelper FormHelper = new WindowsFormHelper();

        Task mainTask;

        bool RunningFlag = false;

        public bool GetRunningFlag()
        {
            return RunningFlag;
        }

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
            Task changeStateTask;

            if (RunningFlag)
            {
                changeStateTask = new Task(() =>
                {
                    FormHelper.SetRichTextBox(txtCrawlingInfoJson, "", false);
                    FormHelper.SetLabel(lblSleepRemain, "");
                    FormHelper.buttonToggle(btnRun, "중지", true);
                });
            }
            else
            {
                changeStateTask = new Task(() =>
                {
                    FormHelper.buttonToggle(btnRun, "중지중...", true);
                    FormHelper.SetLabel(lblSleepRemain, "");
                    FormHelper.SetRichTextBox(txtCrawlingInfoJson, "", true);

                    Task.WaitAny(mainTask);

                    FormHelper.buttonToggle(btnRun, "실행", true);
                });
            }

            changeStateTask.Start();
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
            var itemStatus = lstItem.GroupBy(x => new { x.ItemTitle, x.ItemNumber })
                .Select(x => new
                {
                    x.Key.ItemTitle,
                    x.Key.ItemNumber,
                    TotanCnt = x.Count(),
                    CompleteCnt = x.Count(g => g.DownloadComplete == true)
                }).Where(x => x.CompleteCnt != x.TotanCnt)
                .OrderBy(x => x.ItemTitle).ThenBy(x => x.ItemNumber)
                .Select(x => $"{x.ItemTitle} - {x.ItemNumber} : {x.CompleteCnt}/{x.TotanCnt}");

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
                    GetRunningFlag,
                    new Logger(WriteStatus, WriteItem, WriteSleepStatus)
                    );

                RunningFlag = true;
                
                mainTask = Task.Run(crawlerEngine.Run);
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


        private async void Application_ApplicationExit(object sender, EventArgs e)
        {
            RunningFlag = false;
            if (mainTask != null && mainTask.IsCompleted == false)
            {
                await mainTask;
            }
        }
    }
}