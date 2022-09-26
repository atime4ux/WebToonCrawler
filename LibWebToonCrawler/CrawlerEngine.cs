using LibWebToonCrawler.Base;
using LibWebToonCrawler.Model;
using LibWebToonCrawler.Parser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace LibWebToonCrawler
{
    public class CrawlerEngine : BaseEngine<CrawlingItem>
    {
        private List<CrawlingInfo> LstCrawlingInfo { get; set; }

        public Func<bool> GetRunningFlag { get; set; }

        public CrawlerEngine(string json
            , Func<bool> getRunningFlag
            , Logger logAction = null)
        {
            try
            {
                LstCrawlingInfo = JsonConvert.DeserializeObject<List<CrawlingInfo>>(json);
            }
            catch (Exception ex)
            { }

            GetRunningFlag = getRunningFlag ?? (() => { return true; });

            var lstParsingModule = new List<IParser<CrawlingItem>>();
            lstParsingModule.Add(new Toonkor(LstCrawlingInfo, GetRunningFlag, logAction.WriteStatus));

            base.Init(null, logAction, lstParsingModule);
        }

        private void RunParser()
        {
            LogAction.WriteStatus("start");

            foreach (var info in LstCrawlingInfo)
            {
                //작업 수행
                if (GetRunningFlag())
                {
                    SiteName siteName;
                    if (Enum.TryParse<SiteName>(info.SiteName, out siteName))
                    {
                        var parsing = LstParsingModule.FirstOrDefault(x => x.GetParsingTarget() == siteName);
                        if (parsing != null)
                        {
                            LogAction.WriteStatus($"downloading [{info.SiteName}] data");

                            List<CrawlingItem> lstItem = parsing.GetParsingList();

                            LogAction.WriteStatus($"{parsing.GetParsingTarget()} : success get {lstItem.Count} items");
                            LogAction.WriteItem(lstItem);

                            //다운로드 처리
                            Download(lstItem);
                        }
                    }
                }
            }

            LogAction.WriteStatus("finish\r\n====================");
        }

        private List<CrawlingItem> lstDownloadStatus = new List<CrawlingItem>();
        private void Download(List<CrawlingItem> lstAllItem)
        {
            int maxAsyncJob = 10;

            List<Task> lstTask = new List<Task>();
            foreach (string title in lstAllItem.GroupBy(x => x.ItemTitle).Select(x => x.Key))
            {
                foreach (string number in lstAllItem.Where(x => x.ItemTitle == title).GroupBy(x => x.ItemNumber).Select(x => x.Key))
                {
                    //제목, 회차별 비동기 다운로드
                    if (GetRunningFlag())
                    {
                        lstTask.Add(Task.Factory.StartNew(() => {
                            List<CrawlingItem> lstAsyncGroup = lstAllItem.Where(x => x.ItemTitle == title && x.ItemNumber == number).ToList();

                            LogAction.WriteStatus($"download async start : {lstAsyncGroup[0].ItemId}");
                            DownloadNumberOfTitle(lstAsyncGroup, lstAllItem);
                            LogAction.WriteStatus($"download async end : {lstAsyncGroup[0].ItemId}");
                        }));

                        if (lstTask.Count == maxAsyncJob)
                        {
                            //최대 10개까지 비동기 작업
                            Task.WaitAny(lstTask.ToArray());
                            lstTask.RemoveAll(x => x.IsCompleted);
                        }
                    }
                }
            }

            Task.WaitAll(lstTask.ToArray());
        }

        private void DownloadNumberOfTitle(List<CrawlingItem> lstItem, List<CrawlingItem> lstAllItem)
        {
            try
            {
                if (lstItem.Count > 0)
                {
                    string baseDir = $"{System.IO.Directory.GetCurrentDirectory()}\\download\\{lstItem[0].ItemTitle}";

                    Func<CrawlingItem, string> getDownlaodPath = (ci) =>
                    {
                        string path = $"{baseDir}\\{Helper.CommonHelper.RemoveInvalidFileNameChars(ci.ItemId)}";
                        if (System.IO.Directory.Exists(path) == false)
                        {
                            System.IO.Directory.CreateDirectory(path);
                        }

                        return path;
                    };

                    Func<CrawlingItem, int, string> getImageFilePath = (ci, i) =>
                    {
                        string path = getDownlaodPath(ci);
                        string ext = System.IO.Path.GetExtension(ci.ItemUrl).Replace(".", "");
                        string fileName = $"{(i + 1).ToString().PadLeft(5, '0')}.{ext}";

                        return $"{path}\\{fileName}";
                    };

                    Action<CrawlingItem> zipImg = (ci) =>
                    {
                        //압축
                        string srcPath = getDownlaodPath(ci);
                        string destPath = $"{baseDir}\\{Helper.CommonHelper.RemoveInvalidFileNameChars(ci.ItemId)}.zip";

                        ZipFile.CreateFromDirectory(srcPath, destPath);

                        //압축 후 삭제
                        System.IO.Directory.Delete(srcPath, true);
                    };

                    using (var webClient = new System.Net.WebClient())
                    {
                        webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");

                        int fileIdx = 0;
                        foreach (var item in lstItem)
                        {
                            if (GetRunningFlag())
                            {
                                if (fileIdx == 0)
                                {
                                    LogAction.WriteStatus($"download img start : {item.ItemId}");
                                }

                                try
                                {
                                    //await webClient.DownloadFileTaskAsync(item.ItemUrl, getImageFilePath(item, fileIdx));
                                    webClient.DownloadFile(item.ItemUrl, getImageFilePath(item, fileIdx));
                                    item.DownloadComplete = true;

                                    LogAction.WriteItem(lstAllItem);
                                }
                                catch (Exception ex)
                                {
                                    LogAction.WriteStatus($"download img error : {item.ItemId} - {fileIdx + 1} - {item.ItemUrl}\r\n{ex.Message}");
                                }

                                if (item == lstItem.Last())
                                {
                                    //마지막 or 다음 항목과 다른 경우 압축 후 삭제
                                    zipImg(item);
                                    LogAction.WriteStatus($"download img end : {item.ItemId}");

                                    //마커 초기화
                                    fileIdx = 0;
                                }
                                else
                                {
                                    fileIdx++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            { }
            finally
            { }
        }

        public void Run()
        {
            RunParser();
        }

        public static List<CrawlingInfo> GetSampleCrawlingInfo()
        {
            var sample = new List<CrawlingInfo>();
            sample.Add(new CrawlingInfo()
            {
                SiteName = "툰코",
                Title = "제목1",
                IndexUrl = "https://abc.com/def",
                StartIdx = 1,
                EndIdx = 10
            });
            sample.Add(new CrawlingInfo()
            {
                SiteName = "툰코",
                Title = "제목2",
                IndexUrl = "https://abc.com/def2",
                StartIdx = 1,
                EndIdx = 10
            });

            return sample;
        }
    }

    public class Logger : BaseLog<CrawlingItem>
    {
        public Logger(Action<string> writeStatus
            , Action<List<CrawlingItem>> writeItem
            , Action<string> writeSleepStatus) : base(writeStatus, writeItem, writeSleepStatus)
        { }
    }
}
