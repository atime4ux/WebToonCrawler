using System;
using LibWebToonCrawler.Helper;
using System.Collections.Generic;
using System.Linq;

namespace LibWebToonCrawler.Model
{
    public class CrawlingInfo
	{
        public string SiteName { get; set; }
        public string Title { get; set; }
        public string IndexUrl { get; set; }
        public string[] InputIndex { get; set; }
        public string RetryFailIndex { get; set; }
        
        private int[] _index = null;
        public int[] GetIndex()
        {
            if (_index == null)
            {
                List<int> lstResult = new List<int>();

                if (RetryFailIndex == "Y")
                {
                    lstResult = LoadFailIndex();
                }
                else
                {
                    foreach (string targetIndex in InputIndex)
                    {
                        if (targetIndex.IndexOf("-") > 0 && targetIndex.Split('-').Length == 2)
                        {
                            var idxRange = targetIndex.Split('-').Select(x =>
                            {
                                int tmp = -1;
                                int.TryParse(x, out tmp);
                                return tmp;
                            });

                            if (idxRange.All(x => x >= 0))
                            {
                                int idxMin = idxRange.Min();
                                int idxMax = idxRange.Max();
                                lstResult.AddRange(Enumerable.Range(idxMin, (idxMax - idxMin) + 1));
                            }
                        }
                        else
                        {
                            int idx = -1;
                            if (int.TryParse(targetIndex, out idx))
                            {
                                lstResult.Add(idx);
                            }
                        }
                    }
                }

                _index = lstResult.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
            }

            return _index;
        }

        public bool BetweenIndex(int idx)
        {
            return GetIndex().Contains(idx);
        }

        private string GetDownloadPath()
        { 
            return $"{CrawlerEngine.DownloadPath}\\{CommonHelper.RemoveInvalidFileNameChars(Title)}";
        }

        private List<int> LoadFailIndex()
        {
            string[] arrIndexDirectory = System.IO.Directory.GetDirectories(GetDownloadPath())
                .Select(x=> System.IO.Path.GetFileName(x)).ToArray();
            return arrIndexDirectory.Select(x => Convert.ToInt32(x.Split('_')[0])).ToList();
        }
    }
}
