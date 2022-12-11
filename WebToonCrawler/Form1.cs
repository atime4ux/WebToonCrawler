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

            lblDownloadSpeed.Text = "";
        }


        private void ChangeRunningState(bool runningFlag)
        {
            RunningFlag = runningFlag;

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
                    FormHelper.buttonToggle(btnRun, "중지중...", false);
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
            CommonHelper.WriteLog(status);

            if (txtLog.Text.Length > 8000)
            {
                FormHelper.SetTextBox(txtLog, "", "N");
            }

            FormHelper.SetTextBox(txtLog, status + "\r\n", "Y");
        }

        private void WriteDownloadSpeed(string str)
        {
            FormHelper.SetLabel(lblDownloadSpeed, str);
        }

        private void WriteItem(List<CrawlingItem> lstItem)
        {
            var itemStatus = lstItem.GroupBy(x => new { x.ItemTitle, x.ItemNumber })
                .Select(x => new
                {
                    x.Key.ItemTitle,
                    x.Key.ItemNumber,
                    TotalCnt = x.Count(),
                    SuccessCnt = x.Count(g => g.DownloadSuccess == true),
                    FailCnt = x.Count(g => g.DownloadFail == true)
                })
                .GroupBy(x => x.ItemTitle)
                .Select(x => new
                {
                    ItemTitle = x.Key,
                    TotalIndex = x.Count(),
                    CompleteIndex = x.Count(g => (g.SuccessCnt + g.FailCnt) == g.TotalCnt),
                    TotalCnt = x.Sum(g => g.TotalCnt),
                    SuccessCnt = x.Sum(g => g.SuccessCnt),
                    FailCnt = x.Sum(g => g.FailCnt)
                })
                .Where(x => x.SuccessCnt != x.TotalCnt)
                .OrderByDescending(x =>
                {
                    if (x.SuccessCnt > 0)
                    {
                        if ((x.SuccessCnt + x.FailCnt) != x.TotalCnt)
                        {
                            //진행중
                            return 20;
                        }
                        else
                        {
                            //완료(실패존재)
                            return 0;
                        }
                    }
                    else
                    {
                        //미진행
                        return 10;
                    }
                })
                .ThenBy(x => x.ItemTitle)
                .Select(x =>
                {
                    string strFailCnt = x.FailCnt > 0 ? $"(Fail:{x.FailCnt})" : "";
                    return $"{x.ItemTitle} [index:{x.CompleteIndex}/{x.TotalIndex}] [images:{x.SuccessCnt}/{x.TotalCnt}]{strFailCnt}";
                });

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

                ChangeRunningState(true);

                RunTask();
            }
            else
            {
                //stop
                ChangeRunningState(false);
            }
        }

        private async void RunTask()
        {
            CrawlerEngine crawlerEngine = new CrawlerEngine(
                    CrawlingInfoJson,
                    GetRunningFlag,
                    new Logger(WriteStatus, WriteItem, WriteSleepStatus, WriteDownloadSpeed)
                    );

            mainTask = Task.Run(crawlerEngine.Run);

            try
            {
                await mainTask;
            }
            catch (Exception ex)
            {
                WriteStatus(ex.Message);
            }

            ChangeRunningState(false);
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