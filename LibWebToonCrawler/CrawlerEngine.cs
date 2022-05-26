using LibWebToonCrawler.Model;
using LibWebToonCrawler.Parser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using LibWebToonCrawler.Base;

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

                        List<CrawlingItem> lstAll = await parsing.GetParsingList();

                        LogAction.WriteStatus($"{parsing.GetParsingTarget()} : success get {lstAll.Count} items");
                        LogAction.WriteItem(lstAll);

                        //다운로드 처리
                        using (var webClient = new System.Net.WebClient())
                        {
                            webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");

                            string baseDir = System.IO.Directory.GetCurrentDirectory();
                            int idx = 0;
                            int prevNumber = 0;
                            foreach (var item in lstAll)
                            {
                                if (prevNumber != item.ItemNumber)
                                {
                                    if (prevNumber > 0)
                                    {
                                        var prevItem = lstAll.First(x => x.ItemNumber == prevNumber);
                                        LogAction.WriteStatus($"download img end : {prevItem.ItemTitle} - {prevItem.ItemNumber}");
                                    }

                                    //초기화
                                    idx = 0;
                                    prevNumber = item.ItemNumber;

                                    LogAction.WriteStatus($"download img start : {item.ItemTitle} - {item.ItemNumber}");
                                }
                                idx++;

                                string itemDir = $"{baseDir}\\{item.ItemTitle}-{item.ItemNumber}";
                                if (System.IO.Directory.Exists(itemDir) == false)
                                {
                                    System.IO.Directory.CreateDirectory(itemDir);
                                }

                                string imgExt = System.IO.Path.GetExtension(item.ItemUrl).Replace(".", "");
                                string imgFileName = $"{idx.ToString().PadLeft(5, '0')}.{imgExt}";

                                try
                                {
                                    webClient.DownloadFile(item.ItemUrl, $"{itemDir}\\{imgFileName}");
                                }
                                catch (Exception ex)
                                {
                                    LogAction.WriteStatus($"download img error : {item.ItemTitle} - {item.ItemNumber} - {idx} - {item.ItemUrl}\r\n{ex.Message}");
                                }
                            }
                            
                        }
                    }
                }
            }

            LogAction.WriteStatus("finish\r\n====================");
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
