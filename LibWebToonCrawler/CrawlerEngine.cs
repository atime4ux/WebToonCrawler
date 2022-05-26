using System.IO.Compression;
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
            string baseDir = $"{System.IO.Directory.GetCurrentDirectory()}\\download";

            Func<CrawlingItem, string> getDownlaodPath = (ci) => {
                string path = $"{baseDir}\\{ci.ItemTitle}_{ci.ItemNumber}";
                if (System.IO.Directory.Exists(path) == false)
                {
                    System.IO.Directory.CreateDirectory(path);
                }

                return path;
            };

            Func<CrawlingItem, int, string> getImageFilePath = (ci, i) => {
                string path = getDownlaodPath(ci);
                string ext = System.IO.Path.GetExtension(ci.ItemUrl).Replace(".", "");
                string fileName = $"{(i + 1).ToString().PadLeft(5, '0')}.{ext}";

                return $"{path}\\{fileName}";
            };

            Action<CrawlingItem> zipImg = (pi) => {
                //압축
                string prevPath = getDownlaodPath(pi);
                ZipFile.CreateFromDirectory(prevPath, $"{baseDir}\\{pi.ItemTitle}_{pi.ItemNumber}.zip");

                //압축 후 삭제
                System.IO.Directory.Delete(prevPath, true);
            };

            using (var webClient = new System.Net.WebClient())
            {
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");

                int itemIdx = 0;
                int fileIdx = 0;
                foreach (var item in lstItem)
                {
                    if (fileIdx == 0)
                    {
                        LogAction.WriteStatus($"download img start : {item.ItemTitle} - {item.ItemNumber}");
                    }

                    try
                    {
                        webClient.DownloadFile(item.ItemUrl, getImageFilePath(item, fileIdx));
                    }
                    catch (Exception ex)
                    {
                        LogAction.WriteStatus($"download img error : {item.ItemTitle} - {item.ItemNumber} - {fileIdx + 1} - {item.ItemUrl}\r\n{ex.Message}");
                    }

                    if ((itemIdx + 1) == lstItem.Count
                        || lstItem[itemIdx + 1].ItemTitle != item.ItemTitle
                        || lstItem[itemIdx + 1].ItemNumber != item.ItemNumber)
                    {
                        //마지막 or 다음 항목과 다른 경우 압축 후 삭제
                        zipImg(item);
                        LogAction.WriteStatus($"download img end : {item.ItemTitle} - {item.ItemNumber}");

                        //마커 초기화
                        fileIdx = 0;
                    }
                    else
                    {
                        fileIdx++;
                    }

                    itemIdx++;
                }
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
