using System;

namespace LibWebToonCrawler.Base
{
    public class BaseParser
	{
		protected Action<string> _FuncLog;
		protected void FuncLog(string logMsg)
		{
			if (_FuncLog == null)
			{
				_FuncLog = (x) => { };
			}

			_FuncLog(logMsg);
		}


		/// <summary>
		/// 지난번 실행된 시간
		/// </summary>
		protected DateTime LastRunDate { get; set; }

		protected SiteName _ParsingTarget { get; set; }
		public SiteName ParsingTarget
		{
			get
			{
				return _ParsingTarget;
			}
		}

		protected int SleepSecond { get; set; }

		protected bool IsBlocked { get; set; }

		protected string ConfigFileName
		{
			get
			{
				return $"{ParsingTarget}.config";
			}
		}

		protected T LoadParsingConfig<T>()
		{
			dynamic result = null;
			string strJson = Helper.CommonHelper.ReadFile(ConfigFileName);
			if (string.IsNullOrEmpty(strJson) == false)
			{ 
				result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(strJson);
			}

			return result;
		}

		protected void SaveParsingConfig<T>(T objInfo)
		{
			if (objInfo != null)
			{
				string strJson = Newtonsoft.Json.JsonConvert.SerializeObject(objInfo);
				Helper.CommonHelper.OverwriteFile(ConfigFileName, strJson);
			}
		}

		protected T Init<T>(T defaultConfig, Action<string> funcLog = null)
		{
			_FuncLog = funcLog;

			T parsingConfig = LoadParsingConfig<T>();
			if (parsingConfig == null)
			{
				parsingConfig = defaultConfig;
				SaveParsingConfig(parsingConfig);
			}

			return parsingConfig;
		}

		public BaseParser(SiteName siteName)
		{
			_ParsingTarget = siteName;
			SleepSecond = 10;
			LastRunDate = DateTime.Now;
		}

		public SiteName GetParsingTarget()
		{
			return ParsingTarget;
		}

		public void SetSleepSecond(int sec)
		{
			SleepSecond = sec;
		}

		public int GetSleepSecond()
		{
			return SleepSecond;
		}
	}
}
