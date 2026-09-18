namespace Admin.NET.Core.Utils
{
    public class DateTimeHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="days"></param>
        /// <returns></returns>
        public static DateTime GetBeginTime(DateTime? dateTime, int days = 0)
        {
            if (dateTime == DateTime.MinValue || dateTime == null)
            {
                return DateTime.Now.AddDays(days);
            }

            return dateTime ?? DateTime.Now;
        }

        #region 时间戳转换

        /// <summary>
        ///  时间戳转本地时间-时间戳精确到秒
        /// </summary> 
        public static DateTime ToLocalTimeDateBySeconds(long unix)
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(unix);
            return dto.ToLocalTime().DateTime;
        }

        /// <summary>
        ///  时间转时间戳Unix-时间戳精确到秒
        /// </summary> 
        public static long ToUnixTimestampBySeconds(DateTime dt)
        {
            DateTimeOffset dto = new DateTimeOffset(dt);
            return dto.ToUnixTimeSeconds();
        }

        /// <summary>
        ///  时间戳转本地时间-时间戳精确到毫秒
        /// </summary> 
        public static DateTime ToLocalTimeDateByMilliseconds(long unix)
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(unix);
            return dto.ToLocalTime().DateTime;
        }

        /// <summary>
        ///  时间戳Unix-时间戳精确到毫秒
        /// </summary> 
        public static DateTime ToUnixTimestampByMilliseconds(long unix)
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(unix);
            return dto.UtcDateTime;
        }

        /// <summary>
        ///  时间转时间戳Unix-时间戳精确到毫秒
        /// </summary> 
        public static long ToUnixTimestampByMilliseconds(DateTime dt)
        {
            DateTimeOffset dto = new DateTimeOffset(dt);
            return dto.ToUnixTimeMilliseconds();
        }

        #endregion

        #region 毫秒转天时分秒

        /// <summary>
        /// 毫秒转天时分秒
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        public static string FormatTime(long ms)
        {
            int ss = 1000;
            int mi = ss * 60;
            int hh = mi * 60;
            int dd = hh * 24;

            long day = ms / dd;
            long hour = (ms - day * dd) / hh;
            long minute = (ms - day * dd - hour * hh) / mi;
            long second = (ms - day * dd - hour * hh - minute * mi) / ss;
            long milliSecond = ms - day * dd - hour * hh - minute * mi - second * ss;

            string sDay = day < 10 ? "0" + day : "" + day; //天
            string sHour = hour < 10 ? "0" + hour : "" + hour; //小时
            string sMinute = minute < 10 ? "0" + minute : "" + minute; //分钟
            string sSecond = second < 10 ? "0" + second : "" + second; //秒
            string sMilliSecond = milliSecond < 10 ? "0" + milliSecond : "" + milliSecond; //毫秒
            sMilliSecond = milliSecond < 100 ? "0" + sMilliSecond : "" + sMilliSecond;

            return string.Format("{0} 天 {1} 小时 {2} 分 {3} 秒", sDay, sHour, sMinute, sSecond);
        }

        #endregion

        #region 获取unix时间戳

        /// <summary>
        /// 获取unix时间戳(毫秒)
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static long GetUnixTimeStamp(DateTime dt)
        {
            long unixTime = ((DateTimeOffset)dt).ToUnixTimeMilliseconds();
            return unixTime;
        }

        public static long GetUnixTimeSeconds(DateTime dt)
        {
            long unixTime = ((DateTimeOffset)dt).ToUnixTimeSeconds();
            return unixTime;
        }

        #endregion

        #region 获取日期天的最小时间

        public static DateTime GetDayMinDate(DateTime dt)
        {
            DateTime min = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0);
            return min;
        }

        #endregion

        #region 获取日期天的最大时间

        public static DateTime GetDayMaxDate(DateTime dt)
        {
            DateTime max = new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59);
            return max;
        }

        #endregion

        #region 获取日期天的最大时间

        public static string FormatDateTime(DateTime? dt)
        {
            if (dt != null)
            {
                if (dt.Value.Year == DateTime.Now.Year)
                {
                    return dt.Value.ToString("MM-dd HH:mm");
                }
                else
                {
                    return dt.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }

            return string.Empty;
        }

        #endregion

        #region 验证是否HH:mm:ss或HH:mm格式

        public static bool IsValidTimeFormat(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
            // 匹配 HH:mm:ss 或 HH:mm
            string pattern = @"^([01][0-9]|2[0-3]):[0-5][0-9](:[0-5][0-9])?$";
            return Regex.IsMatch(input, pattern);
        }

        #endregion

        #region 生成随机时间

        /// <summary>
        /// 计算指定时间范围内的开始和结束时间（支持跨日期）
        /// </summary>
        /// <param name="startHour">开始小时(0-23)</param>
        /// <param name="endHour">结束小时(0-23)，如果小于startHour则表示次日</param>
        /// <param name="baseDate">基准日期，为null时使用当前日期</param>
        /// <param name="offsetMinutes">当当前时间超过开始时间时的偏移分钟数</param>
        /// <returns>返回(开始时间, 结束时间)的元组</returns>
        public static (DateTime start, DateTime end) CalculateTimeRange(int startHour, int endHour,
            DateTime? baseDate = null, int offsetMinutes = 30)
        {
            var now = DateTime.Now;
            var targetDate = baseDate ?? now.Date;

            DateTime start, end;

            if (now.Hour < endHour && endHour < startHour)
            {
                // 当前时间在结束时间之前，且是跨日情况（如凌晨0-2点）
                start = now.AddMinutes(offsetMinutes);
                end = targetDate.AddHours(endHour);

                // 如果当前时间加偏移后会超过结束时间，则推到下一个周期
                if (start >= end)
                {
                    start = targetDate.AddHours(startHour);
                    end = targetDate.AddDays(1).AddHours(endHour);
                }
            }
            else if (now.Hour >= startHour)
            {
                // 当前时间在开始时间之后
                start = targetDate.AddHours(startHour);
                if (endHour < startHour)
                {
                    // 跨日情况
                    end = targetDate.AddDays(1).AddHours(endHour);
                }
                else
                {
                    // 同日情况
                    end = targetDate.AddHours(endHour);
                }

                // 如果当前时间超过开始时间，调整开始时间
                if (now > start)
                {
                    start = now.AddMinutes(offsetMinutes);
                    // 确保调整后的开始时间不会超过结束时间
                    if (start >= end)
                    {
                        // 推到下一个周期
                        if (endHour < startHour)
                        {
                            start = targetDate.AddDays(1).AddHours(startHour);
                            end = targetDate.AddDays(2).AddHours(endHour);
                        }
                        else
                        {
                            start = targetDate.AddDays(1).AddHours(startHour);
                            end = targetDate.AddDays(1).AddHours(endHour);
                        }
                    }
                }
            }
            else
            {
                // 当前时间在开始时间之前
                start = targetDate.AddHours(startHour);
                if (endHour < startHour)
                {
                    // 跨日情况
                    end = targetDate.AddDays(1).AddHours(endHour);
                }
                else
                {
                    // 同日情况
                    end = targetDate.AddHours(endHour);
                }
            }

            return (start, end);
        }

        /// <summary>
        /// 在指定时间范围内生成随机时间
        /// </summary>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="random">随机数生成器，为null时创建新实例</param>
        /// <returns>随机时间</returns>
        public static DateTime GenerateRandomTime(DateTime start, DateTime end, Random random = null)
        {
            if (start >= end)
            {
                throw new ArgumentException("开始时间必须小于结束时间");
            }

            random ??= new Random();

            // 计算时间范围的总秒数
            double totalSeconds = (end - start).TotalSeconds;

            // 生成随机秒数偏移
            double randomSeconds = random.NextDouble() * totalSeconds;

            // 计算随机时间
            return start.AddSeconds(randomSeconds);
        }

        /// <summary>
        /// 批量生成指定数量的随机时间并打乱顺序
        /// </summary>
        /// <param name="count">生成数量</param>
        /// <param name="startHour">开始小时(0-23)</param>
        /// <param name="endHour">结束小时(0-23)，如果小于startHour则表示次日</param>
        /// <param name="baseDate">基准日期，为null时使用当前日期</param>
        /// <param name="offsetMinutes">当当前时间超过开始时间时的偏移分钟数</param>
        /// <param name="random">随机数生成器，为null时创建新实例</param>
        /// <returns>打乱顺序的随机时间列表</returns>
        public static List<DateTime> GenerateShuffledRandomTimes(int count, int startHour, int endHour, DateTime? baseDate = null, int offsetMinutes = 30, Random random = null)
        {
            if (count <= 0)
            {
                return new List<DateTime>();
            }
    
            random ??= new Random();
            var (start, end) = CalculateTimeRange(startHour, endHour, baseDate, offsetMinutes);
    
            // 生成指定数量的随机时间
            var randomTimes = new List<DateTime>();
            for (int i = 0; i < count; i++)
            {
                randomTimes.Add(GenerateRandomTime(start, end, random));
            }
    
            // 使用 Fisher-Yates 算法打乱顺序
            for (int i = randomTimes.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (randomTimes[i], randomTimes[j]) = (randomTimes[j], randomTimes[i]);
            }
    
            return randomTimes;
        }
        /// <summary>
        /// 生成完全随机的时间
        /// </summary>
        /// <param name="startHour">开始小时</param>
        /// <param name="endHour">结束小时</param>
        /// <param name="baseDate">基准日期</param>
        /// <param name="offsetMinutes">偏移分钟数</param>
        /// <returns>完全随机的时间</returns>
        public static DateTime GenerateFullyRandomTime(int startHour, int endHour, DateTime? baseDate = null, int offsetMinutes = 30)
        {
            // 使用当前时间戳 + 随机数作为种子，确保真正的随机性
            var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var randomSeed = new Random().Next();
            var seed = (int)(timestamp ^ randomSeed);
            var random = new Random(seed);
    
            var (start, end) = CalculateTimeRange(startHour, endHour, baseDate, offsetMinutes);
            return GenerateRandomTime(start, end, random);
        }
        #endregion
        
        /// <summary>
        /// 调整发送时间
        /// </summary>
        /// <returns></returns>
        public static DateTime AdjustForWorkTime(DateTime plannedTime, string workTime)
        {
            // 解析工作时间范围
            var times = workTime.Split('-');
            var workStart = TimeSpan.Parse(times[0]);
            var workEnd = TimeSpan.Parse(times[1]);

            // 简单地将UTC时间+8小时转换为北京时间
            DateTime chinaTime = plannedTime.AddHours(8);
            TimeSpan chinaTimeOfDay = chinaTime.TimeOfDay;

            // 检查北京时间是否在工作时间范围内
            bool isInWorkHours = workStart <= workEnd
                ? chinaTimeOfDay >= workStart && chinaTimeOfDay <= workEnd
                : chinaTimeOfDay >= workStart || chinaTimeOfDay <= workEnd;

            if (!isInWorkHours)
            {
                // 如果不在工作时间范围内，计算下一个工作时间的开始（基于北京时间）
                DateTime nextChinaWorkDay = new DateTime(chinaTime.Year, chinaTime.Month, chinaTime.Day);

                // 如果当前北京时间已经超过了今天的工作结束时间，则设为明天的工作开始时间
                if (chinaTimeOfDay > workEnd && workStart <= workEnd)
                {
                    nextChinaWorkDay = nextChinaWorkDay.AddDays(1);
                }

                // 创建下一个有效的工作时间点（北京时间）
                DateTime nextChinaWorkTime = nextChinaWorkDay.Add(workStart);

                // 确保下一个工作时间点不早于现在的北京时间
                if (nextChinaWorkTime < chinaTime)
                {
                    nextChinaWorkTime = nextChinaWorkTime.AddDays(1);
                }

                // 将北京工作时间-8小时转回UTC时间
                return nextChinaWorkTime.AddHours(-8);
            }

            // 如果已经在工作时间内，则直接返回原计划时间
            return plannedTime;
        }
    }
}
