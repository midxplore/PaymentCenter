// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Globalization;

namespace Admin.NET.Core;

public class IdCardHelper
{
    /// <summary>
    /// 验证身份证合理性
    /// </summary>
    /// <param name="idNumber"></param>
    /// <returns></returns>
    public static bool CheckIdCard(string idNumber)
    {
        return CheckIdCard18(idNumber);
    }

    /// <summary>
    /// 18位身份证号码验证(该实现严格遵循GB11643-1999标准)
    /// </summary>
    private static bool CheckIdCard18(string idNumber)
    {
        // 检查长度是否为18位
        if (idNumber?.Length != 18) return false;

        // 数字验证：前17位必须为数字，最后一位为数字或X/x
        for (int i = 0; i < 17; i++)
        {
            if (!char.IsDigit(idNumber[i]))
                return false;
        }
        char lastChar = idNumber[17];
        if (!char.IsDigit(lastChar) && char.ToUpperInvariant(lastChar) != 'X')
            return false;

        // 省份验证
        string[] provinceCodes = { "11", "22", "35", "44", "53", "12", "23", "36", "45", "54", "13",
                              "31", "37", "46", "61", "14", "32", "41", "50", "62", "15", "33",
                              "42", "51", "63", "21", "34", "43", "52", "64", "65", "71", "81", "82", "91" };
        if (!provinceCodes.Contains(idNumber.Substring(0, 2)))
            return false;

        // 生日验证（使用明确格式）
        if (!DateTime.TryParseExact(
            $"{idNumber.Substring(6, 4)}-{idNumber.Substring(10, 2)}-{idNumber.Substring(12, 2)}",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _))
        {
            return false;
        }

        // 校验码验证
        int[] weightFactors = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
        char[] checkCodes = "10X98765432".ToCharArray();

        int sum = 0;
        for (int i = 0; i < 17; i++)
        {
            sum += weightFactors[i] * (idNumber[i] - '0');
        }

        int checkIndex = sum % 11;
        char expectedCode = checkCodes[checkIndex];

        // 不区分大小写比较
        if (char.ToUpperInvariant(idNumber[17]) != char.ToUpperInvariant(expectedCode))
            return false;

        return true;
    }

    /// <summary>
    /// 15位身份证号码验证
    /// </summary>
    private static bool CheckIdCard15(string idNumber)
    {
        long n = 0;
        if (long.TryParse(idNumber, out n) == false || n < Math.Pow(10, 14))
        {
            return false; // 数字验证
        }
        string address = "11x22x35x44x53x12x23x36x45x54x13x31x37x46x61x14x32x41x50x62x15x33x42x51x63x21x34x43x52x64x65x71x81x82x91";
        if (address.IndexOf(idNumber.Remove(2)) == -1)
        {
            return false; // 省份验证
        }
        string birth = idNumber.Substring(6, 6).Insert(4, "-").Insert(2, "-");
        DateTime time = new DateTime();
        if (DateTime.TryParse(birth, out time) == false)
        {
            return false; // 生日验证
        }
        return true;
    }

    /// <summary>
    /// 根据身份证号获取性别代码(Code)
    /// </summary>
    /// <param name="idNumber"></param>
    /// <returns></returns>
    public static GenderEnum GetGenderCode(string idNumber)
    {
        string gender = GetGender(idNumber);
        return gender switch
        {
            "男" => GenderEnum.Male,
            "女" => GenderEnum.Female,
            "未知的性别" => GenderEnum.Unknown,
            "未说明的性别" => GenderEnum.Unspecified,
            _ => GenderEnum.Unknown,
        };
    }

    /// <summary>
    /// 根据身份证号获取性别
    /// </summary>
    /// <param name="idNumber"></param>
    /// <returns></returns>
    public static string GetGender(string idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber)) return string.Empty;
        try
        {
            string strSex = "";
            if (idNumber.Length == 18) strSex = idNumber.Substring(14, 3);

            // 性别代码为偶数是女性奇数为男性
            strSex = int.Parse(strSex) % 2 == 0 ? "女" : "男";
            return strSex;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 根据身份证号，计算精确的年龄
    /// </summary>
    /// <param name="idNumber">生日</param>
    /// <returns></returns>
    public static string GetAge(string idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber)) return null;

        try
        {
            DateTime birthDate = DateTime.Today;
            if (idNumber.Length == 18)
            {
                birthDate = new DateTime(
                    int.Parse(idNumber.Substring(6, 4)),
                    int.Parse(idNumber.Substring(10, 2)),
                    int.Parse(idNumber.Substring(12, 2)));
            }
            else if (idNumber.Length == 15)
            {
                birthDate = new DateTime(
                    int.Parse("19" + idNumber.Substring(6, 2)),
                    int.Parse(idNumber.Substring(8, 2)),
                    int.Parse(idNumber.Substring(10, 2)));
            }

            DateTime today = DateTime.Today;
            double totalDays = (today - birthDate).TotalDays;
            double exactAge = totalDays / 365.2425; // 使用天文年平均值
            exactAge = Math.Round(exactAge, 1);

            return exactAge.ToString("0.0");
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 根据身份证号获取出生日期
    /// </summary>
    /// <param name="idNumber"></param>
    /// <returns></returns>
    public static DateTime GetBirthday(string idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber)) return default;

        try
        {
            string birthday = "";
            if (idNumber.Length == 18)
                birthday = idNumber.Substring(6, 4) + "-" + idNumber.Substring(10, 2) + "-" + idNumber.Substring(12, 2);

            if (idNumber.Length == 15)
                birthday = "19" + idNumber.Substring(6, 2) + "-" + idNumber.Substring(8, 2) + "-" + idNumber.Substring(10, 2);

            return DateTime.Parse(birthday);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 根据出生日期，计算精确的年龄
    /// </summary>
    /// <param name="birthDay">生日</param>
    /// <returns></returns>
    public static string GetAgeByBirthDay(string birthDay)
    {
        string age = "";
        try
        {
            DateTime birthDate = DateTime.Parse(birthDay);
            DateTime nowDateTime = DateTime.Now;
            int _age = nowDateTime.Year - birthDate.Year;
            // 再考虑月、天的因素
            if (nowDateTime.Month < birthDate.Month || (nowDateTime.Month == birthDate.Month && nowDateTime.Day < birthDate.Day))
            {
                _age--;
            }
            return _age.ToString();
        }
        catch
        {
            return age;
        }
    }
}