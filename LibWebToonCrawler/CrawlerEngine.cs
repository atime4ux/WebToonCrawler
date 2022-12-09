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
        public static readonly string DownloadPath = $"{System.IO.Directory.GetCurrentDirectory()}\\download";

        public static string GetDownloadBasePath(string title)
        {
            return $"{DownloadPath}\\{title}";
        }

        public static string GetDownloadPath(string title, string indexName)
        {
            return $"{GetDownloadBasePath(title)}\\{Helper.CommonHelper.RemoveInvalidFileNameChars(indexName)}";
        }

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
                        LogAction.WriteStatus($"downloading [{siteName}] data");

                        List<CrawlingItem> lstItem = parsing.GetParsingList();

                        LogAction.WriteStatus($"{siteName} : success get {lstItem.Count} items");
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
            int maxAsyncJob = 3;
            double avgByteSec = 0;
            int jobIncreaseStep = 5;
            List<Task<double>> lstTask = new List<Task<double>>();

            Action<int> funcTaskComplete = (i) =>
            {
                var completeTask = lstTask[i];
                double byteSec = completeTask.Result;

                if (byteSec > 0)
                {
                    if (avgByteSec <= byteSec)
                    {
                        if (limitAsyncJob > maxAsyncJob)
                        {
                            if (limitAsyncJob >= maxAsyncJob + jobIncreaseStep)
                            {
                                //속도 감소가 없을때까지 다중 다운로드 증가
                                maxAsyncJob += jobIncreaseStep;
                            }
                            else
                            {
                                maxAsyncJob = limitAsyncJob;
                            }
                        }
                    }
                    else
                    {
                        //속도감소 -> 다운로드 감소
                        if (maxAsyncJob > 2)
                        {
                            maxAsyncJob--;
                        }
                        jobIncreaseStep = 1;
                    }

                    avgByteSec = avgByteSec == 0 ? byteSec : new double[] { avgByteSec, byteSec }.Average();

                    double avgKbSec = Math.Round(avgByteSec / 1024.0, 2);
                    LogAction.WriteDownloadSpeed($"{avgKbSec} KB/Sec - {maxAsyncJob} thread running");
                }

                lstTask.RemoveAll(x => x.IsCompleted);
            };

            List<string> lstTitle = lstAllItem.GroupBy(x => x.ItemTitle).Select(x => x.Key).ToList();
            foreach (string title in lstTitle)
            {
                List<string> lstItemNumber = lstAllItem.Where(x => x.ItemTitle == title).GroupBy(x => x.ItemNumber).Select(x => x.Key).ToList();
                foreach (string itemNumber in lstItemNumber)
                {
                    //제목, 회차별 비동기 다운로드
                    if (GetLoopFlag() == false)
                    {
                        break;
                    }

                    List<CrawlingItem> lstNumberOfTitle = lstAllItem.Where(x => x.ItemTitle == title && x.ItemNumber == itemNumber).ToList();

                    //task 개수 맞춰질때까지 대기
                    while (lstTask.Count >= maxAsyncJob)
                    {
                        int taskIdx = Task.WaitAny(lstTask.ToArray());
                        funcTaskComplete(taskIdx);
                    }
                    lstTask.Add(DownloadNumberOfTitleImg(lstNumberOfTitle, lstAllItem));


                    if (lstTask.Count == maxAsyncJob)
                    {
                        //최대 {maxAsyncJob}개까지 비동기 작업
                        int taskIdx = Task.WaitAny(lstTask.ToArray());
                        funcTaskComplete(taskIdx);
                    }
                }
            }

            if (lstTask.Count > 0)
            {
                Task.WaitAll(lstTask.ToArray());
            }
        }

        /// <summary>
        /// 이미 존재하는 파일 스킵, zip파일 있는 경우 0 반환
        /// </summary>
        /// <param name="lstItem"></param>
        /// <param name="lstAllItem"></param>
        /// <returns></returns>
        private async Task<double> DownloadNumberOfTitleImg(List<CrawlingItem> lstItem, List<CrawlingItem> lstAllItem)
        {
            string itemId = "";
            string itemTitle = "";
            double totalByteSec = 0;
            long totalFileSize = 0;
            double totalDownloadSec = 0;
            string[] arrImgExt = new string[] { "jpg", "bmp", "gif", "png" };

            try
            {
                itemId = lstItem[0].ItemId;
                itemTitle = lstItem[0].ItemTitle;

                Func<string, string> getDownlaodPath = (id) =>
                {
                    string path = GetDownloadPath(itemTitle, id);
                    if (System.IO.Directory.Exists(path) == false)
                    {
                        System.IO.Directory.CreateDirectory(path);
                    }

                    return path;
                };

                Func<CrawlingItem, int, string> getImageFilePath = (ci, i) =>
                {
                    string path = getDownlaodPath(ci.ItemId);
                    string ext = System.IO.Path.GetExtension(ci.ItemUrl).Replace(".", "");
                    if (arrImgExt.Contains(ext.ToLower()) == false)
                    {
                        ext = arrImgExt[0];
                    }
                    string fileName = $"{(i + 1).ToString().PadLeft(5, '0')}.{ext}";

                    return $"{path}\\{fileName}";
                };

                Func<string, string> getZipFilePath = (id) =>
                {
                    return $"{GetDownloadPath(itemTitle, id)}.zip";
                };

                Action<string> zipImg = (id) =>
                {
                    //압축
                    string srcPath = getDownlaodPath(id);
                    string destPath = getZipFilePath(id);

                    ZipFile.CreateFromDirectory(srcPath, destPath);

                    //압축 후 삭제
                    System.IO.Directory.Delete(srcPath, true);
                };


                if (new System.IO.FileInfo(getZipFilePath(itemId)).Exists == false)
                {
                    LogAction.WriteStatus($"download img start : {itemId}");

                    int fileIdx = 0;
                    foreach (var item in lstItem)
                    {
                        if (GetLoopFlag() == false)
                        {
                            break;
                        }

                        long fileSize;
                        try
                        {
                            bool downloadFlag = true;

                            string imageFilePath = getImageFilePath(item, fileIdx);
                            var imageFileInfo = new System.IO.FileInfo(imageFilePath);
                            if (imageFileInfo.Exists && imageFileInfo.Length > 0)
                            {
                                //using (var webClient = new System.Net.WebClient())
                                //{
                                //    webClient.OpenRead(item.ItemUrl);
                                //    fileSize = Convert.ToInt64(webClient.ResponseHeaders["Content-Length"]);
                                //    if (fileSize == imageFileInfo.Length)
                                //    {
                                downloadFlag = false;
                                //    }
                                //}
                            }

                            if (downloadFlag == true)
                            {
                                using (var webClient = new System.Net.WebClient())
                                {
                                    webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");

                                    DateTime startDatetime = DateTime.Now;
                                    await webClient.DownloadFileTaskAsync(item.ItemUrl, imageFilePath);
                                    DateTime endDatetime = DateTime.Now;

                                    imageFileInfo.Refresh();
                                    fileSize = imageFileInfo.Length;

                                    totalFileSize += fileSize;
                                    totalDownloadSec += (endDatetime - startDatetime).TotalMilliseconds / 1000.0;

                                    item.DownloadSuccess = true;
                                }
                            }
                            else
                            {
                                item.DownloadSuccess = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogAction.WriteStatus($"download img error : {itemId} - {fileIdx + 1} - {item.ItemUrl}\r\n{ex.Message}");
                            fileSize = 0;
                            item.DownloadFail = true;
                        }

                        LogAction.WriteItem(lstAllItem);

                        fileIdx++;
                    }

                    if (lstItem.All(x => x.DownloadSuccess == true))
                    {
                        //오류 없을때만 압축 후 기존 파일 삭제
                        zipImg(itemId);
                    }

                    LogAction.WriteStatus($"download img end : {itemId}");
                }
                else
                {
                    lstItem.ForEach(x => x.DownloadSuccess = true);
                    LogAction.WriteStatus($"skip download : {itemId}");
                }
            }
            catch (Exception ex)
            {
                LogAction.WriteStatus($"download img error : {itemId}");
            }
            finally
            { }

            if (totalDownloadSec > 0)
            {
                totalByteSec = totalFileSize / totalDownloadSec;
            }

            return totalByteSec;
        }

        public static List<CrawlingInfo> GetSampleCrawlingInfo()
        {
            var sample = new List<CrawlingInfo>();
            sample.Add(new CrawlingInfo()
            {
                SiteName = "툰코",
                Title = "제목1",
                IndexUrl = "https://abc.com/def",
                InputIndex = new string[] { "1-3", "5", $"7-{int.MaxValue}" },
                RetryFailIndex = "N"
            });
            sample.Add(new CrawlingInfo()
            {
                SiteName = "툰코",
                Title = "제목2",
                IndexUrl = "https://abc.com/def2",
                InputIndex = new string[] { "1-3", "5", $"7-{int.MaxValue}" },
                RetryFailIndex = "Y"
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
