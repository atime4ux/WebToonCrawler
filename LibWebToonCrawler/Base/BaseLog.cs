using System;
using System.Collections.Generic;

namespace LibWebToonCrawler.Base
{
	public class BaseLog<T>
	{
		public Action<string> WriteStatus { get; }
		public Action<List<T>> WriteItem { get; }
		public Action<string> WriteSleepStatus { get; }
		public Action<string> WriteDownloadSpeed { get; }

		public BaseLog(Action<string> writeStatus
			, Action<List<T>> writeItem
			, Action<string> writeSleepStatus
			, Action<string> writeDownloadSpeed)
		{
			WriteStatus = writeStatus ?? (x => { });
			WriteItem = writeItem ?? (x => { });
			WriteSleepStatus = writeSleepStatus ?? (x => { });
			WriteDownloadSpeed = writeDownloadSpeed ?? (x => { });
		}
	}
}
