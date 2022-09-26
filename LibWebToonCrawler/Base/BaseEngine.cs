using System;
using System.Collections.Generic;

namespace LibWebToonCrawler.Base
{
    public class BaseEngine<T>
    {
        #region 반복 실행 관련 속성

        /// <summary>
        /// mainJob실행여부
        /// </summary>
        protected bool MainJobTrace = false;
        public const int MinSleepSecond = 10;

        protected bool LoopFlag
        {
            get
            {
                bool result = false;

                if (MainJobTrace == false)
                {
                    result = true;
                }
                else
                {
                    if (GetLoopFlag != null)
                    {
                        result = GetLoopFlag();
                    }
                }

                return result;
            }
        }

        protected Func<bool> GetLoopFlag { get; set; }

        #endregion


        #region 초기화 함수

        /// <summary>
        /// 
        /// </summary>
        /// <param name="getLoopFlag">외부에서 정지 신호 수신</param>
        /// <param name="logAction"></param>
        /// <param name="lstParsingModule"></param>
        protected void Init(Func<bool> getLoopFlag, BaseLog<T> logAction, List<IParser<T>> lstParsingModule)
        {
            GetLoopFlag = getLoopFlag ?? (() => { return false; });
            LogAction = logAction ?? new BaseLog<T>(null, null, null);
            LstParsingModule = lstParsingModule;
        }

        #endregion

        #region 공통 기능

        public BaseLog<T> LogAction { get; set; }

        protected List<IParser<T>> LstParsingModule = null;

        #endregion
    }
}
