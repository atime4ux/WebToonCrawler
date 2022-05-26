using System;
using System.Collections.Generic;

namespace LibWebToonCrawler.Base
{
	public class BaseLog<T>
	{
		public Action<string> WriteStatus { get; set; }
		public Action<List<T>> WriteItem { get; set; }
		public Action<string> WriteSleepStatus { get; set; }
		
		public BaseLog(Action<string> writeStatus
			, Action<List<T>> writeItem
			, Action<string> writeSleepStatus)
		{
			WriteStatus = writeStatus != null ? writeStatus : (x => { });
			WriteItem = writeItem != null ? writeItem : (x => { });
			WriteSleepStatus = writeSleepStatus != null ? writeSleepStatus : (x => { });
		}
	}
}
