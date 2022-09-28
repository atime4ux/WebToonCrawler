using System;

namespace LibWebToonCrawler.Base
{
    public class BaseParser
	{
		private Action<string> _FuncLog;
		protected void FuncLog(string logMsg)
		{
			if (_FuncLog == null)
			{
				_FuncLog = (x) => { };
			}

			_FuncLog(logMsg);
		}

		private Func<bool> _FuncGetRunningFlag;
		protected bool FuncGetRunningFlag()
		{
			if (_FuncGetRunningFlag == null)
			{
				_FuncGetRunningFlag = () => false;
			}

			return _FuncGetRunningFlag();
		}


		/// <summary>
		/// 지난번 실행된 시간
		/// </summary>
		protected DateTime LastRunDate { get; set; }

		protected SiteName _ParsingTarget { get; set; }

		protected int SleepSecond { get; set; }

		protected bool IsBlocked { get; set; }

		protected string ConfigFileName
		{
			get
			{
				return $"{_ParsingTarget}.config";
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

		protected T Init<T>(T defaultConfig, Func<bool> funcGetRunningFlag, Action<string> funcLog = null)
		{
			_FuncLog = funcLog;
			_FuncGetRunningFlag = funcGetRunningFlag;

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
			return _ParsingTarget;
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
