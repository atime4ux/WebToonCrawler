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
            public string PageBaseUrl { get; set; }
            public string ImgBaseUrl { get; set; }
            public string PageUrlFormat { get; set; }
            public string ImgUrlFormat { get; set; }
        }

        public ToonkorConfig DefaultConfig
        {
            get
            {
                return new ToonkorConfig
                {
                    PageBaseUrl = "toonkor112.com",
                    ImgBaseUrl = "toonkor112.com",
                    PageUrlFormat = "https://{0}/{1}_{2}" + System.Web.HttpUtility.UrlEncode("화") + ".html",
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
        public Toonkor(List<CrawlingInfo> lstCrawlingInfo, Action<string> funcLog = null) : base(SiteName.툰코)
        {
            toonkorConfig = Init<ToonkorConfig>(DefaultConfig, funcLog);
            lstToonkorCrawlingInfo = lstCrawlingInfo.Where(x => x.SiteName == GetParsingTarget().ToString()).ToList();
        }

        public string GetPageUrl(string title, int idx)
        {
            return string.Format(toonkorConfig.PageUrlFormat, toonkorConfig.PageBaseUrl, System.Web.HttpUtility.UrlEncode(title), idx);
        }

        public string GetImageUrl(string imgSrc)
        {
            return string.Format(toonkorConfig.ImgUrlFormat, toonkorConfig.ImgBaseUrl, imgSrc);
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


        public async Task<List<CrawlingItem>> GetParsingList()
        {
            Func<CrawlingInfo, bool> CanRun = (crawlingInfo) =>
            {
                bool canRunResult = false;

                if (base.IsBlocked == true)
                {
                    FuncLog("can't run - blocked");
                }
                else if (crawlingInfo.StartIdx < 1 || crawlingInfo.EndIdx < 1)
                {
                    FuncLog("can't run - invalid idx");
                }
                else
                {
                    canRunResult = true;
                }

                return canRunResult;
            };


            Func<string, int, Task<HtmlDocument>> getData = async (title, idx) =>
            {
                HtmlDocument doc = new HtmlDocument();

                try
                {
                    string url = GetPageUrl(title, idx);
                    string strHtml = await CommonHelper.DownloadHtmlString(url);

                    //res.split('var toon_img')[1].split(" = '")[1].split('\';')[0]
                    var splitOpt = StringSplitOptions.None;
                    string strEncode = strHtml.Split(new string[] { "var toon_img = '" }, splitOpt)[1].Split(new string[] { "';" }, splitOpt)[0];
                    string strImgHtml = Decode(strEncode);
                    doc.LoadHtml(strImgHtml);
                }
                catch (Exception ex)
                {
                    CommonHelper.WriteLog(ex.ToString());
                    FuncLog($"err : {ex.Message}");
                }

                if (doc.DocumentNode.HasChildNodes == false)
                {
                    FuncLog($"download data is empty");
                    doc = null;
                }

                return doc;
            };

            base.IsBlocked = false;

            var result = new List<CrawlingItem>();

            foreach (var curCrawlingInfo in lstToonkorCrawlingInfo)
            {
                if (CanRun(curCrawlingInfo) == true)
                {
                    base.LastRunDate = DateTime.Now;

                    for (int curIdx = curCrawlingInfo.StartIdx; curIdx <= curCrawlingInfo.EndIdx; curIdx++)
                    {
                        FuncLog($"{curCrawlingInfo.Title} - downloading page {curIdx}");

                        var lstItem = new List<CrawlingItem>();

                        HtmlDocument domData = await getData(curCrawlingInfo.Title, curIdx);
                        if (domData != null)
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
                                        ItemTitle = curCrawlingInfo.Title,
                                        ItemNumber = curIdx,
                                        ItemUrl = imgUrl,
                                        ItemDesc = alt
                                    });
                                }
                            }
                        }
                        else
                        {
                            FuncLog($"document is null");
                        }

                        if (lstItem.Count == 0)
                        {
                            FuncLog($"blocked");
                            break;
                        }
                        else
                        {
                            result.AddRange(lstItem);
                        }
                    }
                }
            }

            return result;
        }
    }
}
