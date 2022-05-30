namespace LibWebToonCrawler.Model
{
    public class CrawlingItem
    {
        public string ItemTitle { get; set; }
        public string ItemNumber { get; set; }
        public string ItemDesc { get; set; }
        public string ItemUrl { get; set; }

        public string ItemId
        {
            get
            {
                string result;
                if (int.TryParse(ItemNumber, out _))
                {
                    result = $"{ItemTitle}_{ItemNumber}";
                }
                else
                {
                    result = ItemNumber;
                }

                return result;
            }
        }

        public bool DownloadComplete { get; set; }
    }
}
