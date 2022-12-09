using HtmlAgilityPack;
using LibWebToonCrawler.Helper;
using LibWebToonCrawler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LibWebToonCrawler.Base;

namespace LibWebToonCrawler.Parser
{
    /// <summary>
    /// 툰코
    /// </summary>
    public class Toonkor : BaseParser, IParser<CrawlingItem>
    {
        public class ToonkorConfig
        {
            public string PageBaseDomain { get; set; }
            public string ImgBaseDomain { get; set; }
            public string BaseUrlFormat { get; set; }
            public string ImgUrlFormat { get; set; }
        }

        public ToonkorConfig DefaultConfig
        {
            get
            {
                return new ToonkorConfig
                {
                    PageBaseDomain = "toonkor122.com",
                    ImgBaseDomain = "toonkor122.com",
                    BaseUrlFormat = "https://{0}/{1}",
                    ImgUrlFormat = "https://{0}{1}"
                };
            }
        }

        private ToonkorConfig toonkorConfig { get; set; }
        private List<CrawlingInfo> lstToonkorCrawlingInfo { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="funcLog">로그 기록 메소드</param>
        public Toonkor(List<CrawlingInfo> lstCrawlingInfo, Func<bool> funcGetRunningFlag, Action<string> funcLog = null) : base(SiteName.툰코)
        {
            toonkorConfig = Init<ToonkorConfig>(DefaultConfig, funcGetRunningFlag, funcLog);
            lstToonkorCrawlingInfo = lstCrawlingInfo.Where(x => x.SiteName == GetParsingTarget().ToString()).ToList();
        }

        public string GetPageUrl(string title, string path)
        {
            path = string.Join("/", path.Split('/').Where(x => string.IsNullOrEmpty(x) == false));
            return string.Format(toonkorConfig.BaseUrlFormat, toonkorConfig.PageBaseDomain, path);
        }

        public string GetImageUrl(string imgSrc)
        {
            if (imgSrc.ToLower().IndexOf("http") == 0)
            {
                return imgSrc;
            }
            else
            {
                return string.Format(toonkorConfig.ImgUrlFormat, toonkorConfig.ImgBaseDomain, imgSrc);
            }
        }

        private Dictionary<string, string> GetIndex(string url)
        {
            var result = new Dictionary<string, string>();

            HtmlDocument doc = CommonHelper.DownloadHtmlDocument(url);

            HtmlNode table = doc.GetElementbyId("fboardlist").SelectNodes("table").FirstOrDefault(x => x.HasClass("web_list"));
            if (table != null)
            {
                Uri myUri = new Uri(url);
                string host = myUri.Host;

                toonkorConfig.PageBaseDomain = host;
                toonkorConfig.ImgBaseDomain = host;

                foreach (var tr in table.SelectNodes("tr"))
                {
                    var td = tr.SelectNodes("td").FirstOrDefault(x => x.GetAttributeValue("class", "").Split(' ').Contains("content__title"));
                    if (td != null)
                    {
                        string pageName = td.GetAttributeValue("alt", "");
                        if (string.IsNullOrEmpty(pageName))
                        {
                            pageName = td.InnerText.Trim();
                        }

                        string pageUrl = td.GetAttributeValue("data-role", "").Trim();
                        if (string.IsNullOrEmpty(pageUrl) == false
                            && result.ContainsKey(pageName) == false)
                        {
                            result.Add(pageName, pageUrl);
                        }
                    }
                }
            }

            return result.Reverse().ToDictionary(x=> x.Key, x=> x.Value);
        }

        private List<CrawlingItem> GetIndexImageUrl(string pageUrl, string title, string itemNumber)
        {
            Func<string, HtmlDocument> getPageData = (url) =>
            {
                FuncLog($"start[download page data] : {url}");

                HtmlDocument doc = new HtmlDocument();

                try
                {
                    string strHtml = CommonHelper.DownloadHtmlString(url);

                    //res.split('var toon_img')[1].split(" = '")[1].split('\';')[0]
                    var splitOpt = StringSplitOptions.None;
                    string strEncode = strHtml.Split(new string[] { "var toon_img = '" }, splitOpt)[1].Split(new string[] { "';" }, splitOpt)[0];
                    string strImgHtml = Decode(strEncode);
                    doc.LoadHtml(strImgHtml);
                }
                catch (Exception ex)
                {
                    CommonHelper.WriteLog(ex.ToString());
                    FuncLog($"err[download page data] : {ex.Message}");
                }

                if (doc.DocumentNode.HasChildNodes == false)
                {
                    FuncLog($"empty[download page data] : {url}");
                    doc = null;
                }

                return doc;
            };


            var lstItem = new List<CrawlingItem>();
            HtmlDocument domData = getPageData(pageUrl);
            if (domData != null && domData.DocumentNode.ChildNodes.Count > 0)
            {
                foreach (var img in domData.DocumentNode.ChildNodes)
                {
                    string alt = img.GetAttributeValue("alt", "");
                    string src = img.GetAttributeValue("src", "");

                    if (string.IsNullOrEmpty(src) == false)
                    {
                        string imgUrl = GetImageUrl(src);
                        lstItem.Add(new CrawlingItem()
                        {
                            ItemTitle = title,
                            ItemNumber = itemNumber,
                            ItemUrl = imgUrl,
                            ItemDesc = alt
                        });
                    }
                }
            }
            else
            {
                FuncLog($"document is null");

                //폴더 생성용 추가
                lstItem.Add(new CrawlingItem()
                {
                    ItemTitle = title,
                    ItemNumber = itemNumber,
                    ItemUrl = "",
                    ItemDesc = ""
                });
            }

            return lstItem;
        }

        private string Decode(string input)
        {
            string _keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
            int chr1, chr2, chr3;
            int enc1, enc2, enc3, enc4, i = 0;
            string output = "";
            while (i < input.Length)
            {
                enc1 = _keyStr.IndexOf(input[i++]);
                enc2 = _keyStr.IndexOf(input[i++]);
                enc3 = _keyStr.IndexOf(input[i++]);
                enc4 = _keyStr.IndexOf(input[i++]);
                chr1 = ((enc1 << 2) | (enc2 >> 4));
                chr2 = ((enc2 & 15) << 4) | (enc3 >> 2);
                chr3 = ((enc3 & 3) << 6) | enc4;
                output = output + new string(new char[] { (char)chr1 });
                if (enc3 != 64)
                {
                    output = output + new string(new char[] { (char)chr2 });
                }
                if (enc4 != 64)
                {
                    output = output + new string(new char[] { (char)chr3 });
                }
            }
            output = DecodeUtf8(output);
            return output;
        }

        private string DecodeUtf8(string utftext)
        {
            var result = "";
            int i = 0;
            int c = 0, c1 = 0, c2 = 0;
            while (i < utftext.Length)
            {
                c = (int)utftext[i];
                if (c < 128)
                {
                    result += new string(new char[] { (char)c });
                    i++;
                }
                else if ((c > 191) && (c < 224))
                {
                    c2 = (int)utftext[i + 1];
                    result += new string(new char[] { (char)(((c & 31) << 6) | (c2 & 63)) });
                    i += 2;
                }
                else
                {
                    c2 = utftext[i + 1];
                    int c3 = utftext[i + 2];
                    result += new string(new char[] { (char)(((c & 15) << 12) | ((c2 & 63) << 6) | (c3 & 63)) });
                    i += 3;
                }
            }
            return result;
        }


        public List<CrawlingItem> GetParsingList()
        {
            Func<CrawlingInfo, bool> CanRun = (crawlingInfo) =>
            {
                bool canRunResult = false;

                if (base.IsBlocked == true)
                {
                    FuncLog("can't run - blocked");
                }
                else if (crawlingInfo.GetIndex().Length == 0)
                {
                    FuncLog("can't run - invalid idx");
                }
                else
                {
                    canRunResult = true;
                }

                return canRunResult;
            };


            

            base.IsBlocked = false;

            var result = new List<CrawlingItem>();
            string strLock = "";
            var lstTask = new List<Task>();
            int asyncJobCnt = 8;

            foreach (var curCrawlingInfo in lstToonkorCrawlingInfo)
            {
                if (FuncGetRunningFlag() == false)
                {
                    break;
                }

                if (CanRun(curCrawlingInfo) == true)
                {
                    base.LastRunDate = DateTime.Now;

                    //전체 목차 다운로드
                    Dictionary<string, string> dicIndexPath = GetIndex(curCrawlingInfo.IndexUrl);
                    if (dicIndexPath.Count > 0)
                    {
                        if (curCrawlingInfo.GetIndex().Length > 0)
                        {
                            foreach (var indexPath in dicIndexPath.Select((x, i) => new { page = x, index = i + 1 }))
                            {
                                if (FuncGetRunningFlag() == false)
                                {
                                    break;
                                }

                                var page = indexPath.page;
                                int curIdx = indexPath.index;

                                if (curCrawlingInfo.BetweenIndex(curIdx))
                                {
                                    string indexTitle = $"{curIdx.ToString().PadLeft(4, '0')}_{page.Key}";
                                    string path = page.Value;
                                    string url = GetPageUrl(curCrawlingInfo.Title, path);

                                    while (lstTask.Count >= asyncJobCnt)
                                    {
                                        Task.WaitAny(lstTask.ToArray());
                                        lstTask.RemoveAll(x => x.IsCompleted);
                                    }

                                    FuncLog($"{curCrawlingInfo.Title} - downloading page {indexTitle}");

                                    lstTask.Add(Task.Run(() =>
                                    {
                                        List<CrawlingItem> lstItem = GetIndexImageUrl(url, curCrawlingInfo.Title, indexTitle);
                                        if (lstItem.Count == 0)
                                        {
                                            FuncLog($"{curCrawlingInfo.Title} - {curIdx} - empty");
                                        }
                                        else
                                        {
                                            lock (strLock)
                                            {
                                                result.AddRange(lstItem);
                                            }
                                        }
                                    }));
                                }
                            }
                        }
                    }
                    else
                    {
                        FuncLog($"{curCrawlingInfo.Title} - invalid index");
                    }
                }
            }

            if (lstTask.Any(x => x.IsCompleted == false))
            {
                Task.WaitAll(lstTask.ToArray());
            }

            return result;
        }
    }
}
