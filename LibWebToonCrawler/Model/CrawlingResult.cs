using System.Collections.Generic;

namespace LibWebToonCrawler.Model
{
    public class CrawlingResult
	{
        public int Idx { get; set; }
        public List<CrawlingItem> Items { get; set; }

		public CrawlingResult()
		{
			Items = new List<CrawlingItem>();
		}
	}
}
