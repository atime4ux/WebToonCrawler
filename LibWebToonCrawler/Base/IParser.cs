using System.Collections.Generic;
using System.Threading.Tasks;

namespace LibWebToonCrawler.Base
{
    public interface IParser<T>
	{
		SiteName GetParsingTarget();

		void SetSleepSecond(int sec);

		int GetSleepSecond();

		List<T> GetParsingList();
	}

	public enum SiteName
	{
		툰코
	}
}
