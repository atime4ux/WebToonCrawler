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

        public CrawlerEngine(string json
            , Logger logAction = null)
        {
            try
            {
                LstCrawlingInfo = JsonConvert.DeserializeObject<List<CrawlingInfo>>(json);
            }
            catch (Exception ex)
            { }

            var lstParsingModule = new List<IParser<CrawlingItem>>();
            lstParsingModule.Add(new Toonkor(LstCrawlingInfo, logAction.WriteStatus));

            base.Init(null, logAction, lstParsingModule);
        }

        private async void RunParser()
        {
            LogAction.WriteStatus("start");

            foreach (var info in LstCrawlingInfo)
            {
                //작업 수행

                SiteName siteName;
                if (Enum.TryParse<SiteName>(info.SiteName, out siteName))
                {
                    var parsing = LstParsingModule.FirstOrDefault(x => x.GetParsingTarget() == siteName);
                    if (parsing != null)
                    {
                        LogAction.WriteStatus($"downloading [{info.SiteName}] data");

                        List<CrawlingItem> lstItem = await parsing.GetParsingList();

                        LogAction.WriteStatus($"{parsing.GetParsingTarget()} : success get {lstItem.Count} items");
                        LogAction.WriteItem(lstItem);

                        //다운로드 처리
                        Download(lstItem);
                    }
                }
            }

            LogAction.WriteStatus("finish\r\n====================");
        }

        private void Download(List<CrawlingItem> lstItem)
        {
            List<Task> lstTask = new List<Task>();
            foreach (string title in lstItem.GroupBy(x => x.ItemTitle).Select(x => x.Key))
            {
                foreach (string number in lstItem.Where(x => x.ItemTitle == title).GroupBy(x => x.ItemNumber).Select(x => x.Key))
                {
                    //제목, 회차별 비동기 다운로드

                    lstTask.Add(Task.Factory.StartNew(() => {
                        List<CrawlingItem> lstAsyncGroup = lstItem.Where(x => x.ItemTitle == title && x.ItemNumber == number).ToList();

                        LogAction.WriteStatus($"download async start : {lstAsyncGroup[0].ItemId}");
                        DownloadNumberOfTitle(lstAsyncGroup);
                        LogAction.WriteStatus($"download async end : {lstAsyncGroup[0].ItemId}");
                    }));
                }
            }

            Task.WaitAll(lstTask.ToArray());
        }

        private void DownloadNumberOfTitle(List<CrawlingItem> lstItem)
        {
            try
            {
                if (lstItem.Count > 0)
                {
                    string baseDir = $"{System.IO.Directory.GetCurrentDirectory()}\\download\\{lstItem[0].ItemTitle}";

                    Func<CrawlingItem, string> getDownlaodPath = (ci) =>
                    {
                        string path;
                        if (int.TryParse(ci.ItemNumber, out _))
                        {
                            path = $"{baseDir}\\{ci.ItemTitle}_{ci.ItemNumber}";
                        }
                        else
                        {
                            path = $"{baseDir}\\{ci.ItemNumber}";
                        }

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
                        string prevPath = getDownlaodPath(ci);
                        string destPath = $"{baseDir}\\{ci.ItemId}.zip";
                        
                        ZipFile.CreateFromDirectory(prevPath, destPath);

                        //압축 후 삭제
                        System.IO.Directory.Delete(prevPath, true);
                    };

                    using (var webClient = new System.Net.WebClient())
                    {
                        webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");

                        int fileIdx = 0;
                        foreach (var item in lstItem)
                        {
                            if (fileIdx == 0)
                            {
                                LogAction.WriteStatus($"download img start : {item.ItemId}");
                            }

                            try
                            {
                                //await webClient.DownloadFileTaskAsync(item.ItemUrl, getImageFilePath(item, fileIdx));
                                webClient.DownloadFile(item.ItemUrl, getImageFilePath(item, fileIdx));
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
            catch (Exception ex)
            { }
            finally
            {
                //CompleteAsyncJob();
            }
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
                StartIdx = 1,
                EndIdx = 10
            });
            sample.Add(new CrawlingInfo()
            {
                SiteName = "툰코",
                Title = "제목2",
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
