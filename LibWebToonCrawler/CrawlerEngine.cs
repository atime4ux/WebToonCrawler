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
        private List<CrawlingInfo> LstCrawlingInfo { get; }

        public CrawlerEngine(string json
            , Func<bool> getLoopFlag
            , Logger logAction = null)
        {
            try
            {
                LstCrawlingInfo = JsonConvert.DeserializeObject<List<CrawlingInfo>>(json);
            }
            catch (Exception ex)
            { }

            base.Init(getLoopFlag, logAction
                , new List<IParser<CrawlingItem>>() {
                    new Toonkor(LstCrawlingInfo.Where(x=> x.SiteName == "툰코").ToList(), getLoopFlag, logAction.WriteStatus)
                    }
                );
        }

        public void Run()
        {
            LogAction.WriteStatus("start");

            foreach (var info in LstCrawlingInfo)
            {
                //작업 수행
                if (GetLoopFlag() == false)
                {
                    break;
                }

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

            LogAction.WriteStatus("finish\r\n====================");
        }

        private void Download(List<CrawlingItem> lstAllItem)
        {
            int limitAsyncJob = 20;
            int maxAsyncJob = 5;
            double avgByteSec = 0;

            List<Task<double>> lstTask = new List<Task<double>>();
            foreach (string title in lstAllItem.GroupBy(x => x.ItemTitle).Select(x => x.Key))
            {
                foreach (string number in lstAllItem.Where(x => x.ItemTitle == title).GroupBy(x => x.ItemNumber).Select(x => x.Key))
                {
                    //제목, 회차별 비동기 다운로드
                    if (GetLoopFlag() == false)
                    {
                        break;
                    }

                    lstTask.Add(DownloadNumberOfTitle(lstAllItem.Where(x => x.ItemTitle == title && x.ItemNumber == number).ToList(), lstAllItem));

                    if (lstTask.Count == maxAsyncJob)
                    {
                        //최대 {maxAsyncJob}개까지 비동기 작업
                        int taskIdx = Task.WaitAny(lstTask.ToArray());

                        var completeTask = lstTask[taskIdx];

                        double byteSec = completeTask.Result;                        
                        if (avgByteSec == 0 || (avgByteSec * 0.8) <= byteSec)
                        {
                            if (limitAsyncJob >= maxAsyncJob + 5)
                            {
                                //속도 감소가 없을때까지 다중 다운로드 증가
                                maxAsyncJob += 5;
                            }
                        }
                        else
                        {
                            //속도감소 -> 다운로드 감소
                            if (maxAsyncJob > 2)
                            {
                                maxAsyncJob--;
                            }
                        }

                        avgByteSec = avgByteSec == 0 ? byteSec : new double[] { avgByteSec, byteSec }.Average();

                        double avgMbSec = Math.Round(avgByteSec / (double)Math.Pow(1024, 2), 2);
                        LogAction.WriteDownloadSpeed($"{avgMbSec} MB/Sec");

                        lstTask.RemoveAll(x => x.IsCompleted);
                    }
                }
            }

            Task.WaitAll(lstTask.ToArray());
        }

        private async Task<double> DownloadNumberOfTitle(List<CrawlingItem> lstItem, List<CrawlingItem> lstAllItem)
        {
            string itemId = "";
            List<double> lstDownloadSpeed = new List<double>();

            try
            {
                if (lstItem.Count > 0)
                {
                    itemId = lstItem[0].ItemId;

                    LogAction.WriteStatus($"download async start : {itemId}");

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


                    DateTime lastByteReceiveTime = DateTime.Now;
                    long lastBytes = 0;

                    Action<long> progressChanged = (bytes) =>
                    {
                        if (lastBytes != bytes)
                        {
                            var now = DateTime.Now;
                            var timeSpan = now - lastByteReceiveTime;
                            if (timeSpan.Milliseconds > 0)
                            {
                                var bytesChange = bytes - lastBytes;
                                lastBytes = bytes;
                                lastByteReceiveTime = now;

                                double byteSec = Math.Ceiling(bytesChange / (timeSpan.Milliseconds / 1000.0));
                                lstDownloadSpeed.Add(byteSec);
                            }
                        }
                    };


                    using (var webClient = new System.Net.WebClient())
                    {
                        webClient.DownloadProgressChanged += (sender, e) => progressChanged(e.BytesReceived);
                        webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");

                        int fileIdx = 0;
                        foreach (var item in lstItem)
                        {
                            if (GetLoopFlag() == false)
                            {
                                break;
                            }

                            if (fileIdx == 0)
                            {
                                LogAction.WriteStatus($"download img start : {item.ItemId}");
                            }

                            try
                            {
                                lastByteReceiveTime = DateTime.Now;
                                await webClient.DownloadFileTaskAsync(item.ItemUrl, getImageFilePath(item, fileIdx));
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

                    LogAction.WriteStatus($"download async end : {itemId}");
                }
            }
            catch (Exception ex)
            {
                LogAction.WriteStatus($"download async end : {itemId}");
            }
            finally
            { }

            return lstDownloadSpeed.Average();
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
            , Action<string> writeSleepStatus
            , Action<string> writeDownloadSpeed) : base(writeStatus, writeItem, writeSleepStatus, writeDownloadSpeed)
        { }
    }
}
