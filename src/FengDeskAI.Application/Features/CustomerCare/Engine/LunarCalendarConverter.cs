namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Chuyển đổi dương ↔ âm lịch Việt Nam (múi giờ +7) + kinh độ mặt trời (tiết khí).
/// Thuần C#, deterministic, không I/O — theo thuật toán thiên văn của Hồ Ngọc Đức
/// (https://www.informatik.uni-leipzig.de/~duc/amlich/). Dùng cho DestinyCalculator + BaTuCalculator.
/// </summary>
public static class LunarCalendarConverter
{
    /// <summary>Múi giờ Việt Nam.</summary>
    public const double TimeZone = 7.0;

    private const double NewMoonCycle = 29.530588853;
    private const double JdNewMoonEpoch = 2415021.076998695;

    /// <summary>Số ngày Julian (JDN, nguyên) từ ngày dương lịch.</summary>
    public static long JdFromDate(int dd, int mm, int yy)
    {
        long a = (14 - mm) / 12;
        long y = yy + 4800 - a;
        long m = mm + 12 * a - 3;
        long jd = dd + (153 * m + 2) / 5 + 365 * y + y / 4 - y / 100 + y / 400 - 32045;
        if (jd < 2299161)
            jd = dd + (153 * m + 2) / 5 + 365 * y + y / 4 - 32083;
        return jd;
    }

    /// <summary>Ngày dương lịch từ JDN.</summary>
    public static (int Day, int Month, int Year) JdToDate(long jd)
    {
        long a, b, c;
        if (jd > 2299160) // sau 5/10/1582: lịch Gregory
        {
            a = jd + 32044;
            b = (4 * a + 3) / 146097;
            c = a - b * 146097 / 4;
        }
        else
        {
            b = 0;
            c = jd + 32082;
        }
        long d = (4 * c + 3) / 1461;
        long e = c - 1461 * d / 4;
        long m = (5 * e + 2) / 153;
        int day = (int)(e - (153 * m + 2) / 5 + 1);
        int month = (int)(m + 3 - 12 * (m / 10));
        int year = (int)(b * 100 + d - 4800 + m / 10);
        return (day, month, year);
    }

    /// <summary>Thời điểm sóc (new moon) thứ k tính từ 1/1/1900 — trả về JD (số thực, giờ UTC).</summary>
    private static double NewMoon(long k)
    {
        double T = k / 1236.85;
        double T2 = T * T;
        double T3 = T2 * T;
        double dr = Math.PI / 180;
        double jd1 = 2415020.75933 + 29.53058868 * k + 0.0001178 * T2 - 0.000000155 * T3;
        jd1 += 0.00033 * Math.Sin((166.56 + 132.87 * T - 0.009173 * T2) * dr);
        double M = 359.2242 + 29.10535608 * k - 0.0000333 * T2 - 0.00000347 * T3;
        double Mpr = 306.0253 + 385.81691806 * k + 0.0107306 * T2 + 0.00001236 * T3;
        double F = 21.2964 + 390.67050646 * k - 0.0016528 * T2 - 0.00000239 * T3;
        double c1 = (0.1734 - 0.000393 * T) * Math.Sin(M * dr) + 0.0021 * Math.Sin(2 * dr * M);
        c1 = c1 - 0.4068 * Math.Sin(Mpr * dr) + 0.0161 * Math.Sin(dr * 2 * Mpr);
        c1 -= 0.0004 * Math.Sin(dr * 3 * Mpr);
        c1 = c1 + 0.0104 * Math.Sin(dr * 2 * F) - 0.0051 * Math.Sin(dr * (M + Mpr));
        c1 = c1 - 0.0074 * Math.Sin(dr * (M - Mpr)) + 0.0004 * Math.Sin(dr * (2 * F + M));
        c1 = c1 - 0.0004 * Math.Sin(dr * (2 * F - M)) - 0.0006 * Math.Sin(dr * (2 * F + Mpr));
        c1 = c1 + 0.0010 * Math.Sin(dr * (2 * F - Mpr)) + 0.0005 * Math.Sin(dr * (2 * Mpr + M));
        double deltaT = T < -11
            ? 0.001 + 0.000839 * T + 0.0002261 * T2 - 0.00000845 * T3 - 0.000000081 * T * T3
            : -0.000278 + 0.000265 * T + 0.000262 * T2;
        return jd1 + c1 - deltaT;
    }

    /// <summary>JDN (nguyên, theo giờ địa phương) của ngày sóc thứ k.</summary>
    private static long GetNewMoonDay(long k, double timeZone)
        => (long)Math.Floor(NewMoon(k) + 0.5 + timeZone / 24.0);

    /// <summary>Kinh độ mặt trời (radian, 0..2π) tại thời điểm JD (số thực, UTC).</summary>
    public static double SunLongitudeRad(double jdn)
    {
        double T = (jdn - 2451545.0) / 36525;
        double T2 = T * T;
        double dr = Math.PI / 180;
        double M = 357.52910 + 35999.05030 * T - 0.0001559 * T2 - 0.00000048 * T * T2;
        double L0 = 280.46645 + 36000.76983 * T + 0.0003032 * T2;
        double DL = (1.914600 - 0.004817 * T - 0.000014 * T2) * Math.Sin(dr * M);
        DL += (0.019993 - 0.000101 * T) * Math.Sin(dr * 2 * M) + 0.000290 * Math.Sin(dr * 3 * M);
        double L = (L0 + DL) * dr;
        L -= Math.PI * 2 * Math.Floor(L / (Math.PI * 2));
        return L;
    }

    /// <summary>Kinh độ mặt trời (ĐỘ, 0..360) tại trưa địa phương của ngày JDN — đủ chính xác cho ranh giới tiết khí mức ngày.</summary>
    public static double SunLongitudeDeg(long jdn, double timeZone = TimeZone)
        => SunLongitudeRad(jdn - timeZone / 24.0) * 180 / Math.PI;

    /// <summary>Chỉ số cung 30° (0..11) của kinh độ mặt trời đầu ngày JDN — dùng cho thuật toán âm lịch.</summary>
    private static int GetSunLongitudeIndex(long dayNumber, double timeZone)
        => (int)Math.Floor(SunLongitudeRad(dayNumber - 0.5 - timeZone / 24.0) / Math.PI * 6);

    /// <summary>JDN ngày sóc bắt đầu tháng 11 âm của năm dương yy (tháng chứa Đông Chí).</summary>
    private static long GetLunarMonth11(int yy, double timeZone)
    {
        double off = JdFromDate(31, 12, yy) - JdNewMoonEpoch;
        long k = (long)Math.Floor(off / NewMoonCycle);
        long nm = GetNewMoonDay(k, timeZone);
        int sunLong = GetSunLongitudeIndex(nm, timeZone);
        if (sunLong >= 9) nm = GetNewMoonDay(k - 1, timeZone);
        return nm;
    }

    /// <summary>Vị trí tháng nhuận (offset từ tháng 11 âm a11) trong năm âm có 13 tháng.</summary>
    private static long GetLeapMonthOffset(long a11, double timeZone)
    {
        long k = (long)Math.Floor((a11 - JdNewMoonEpoch) / NewMoonCycle + 0.5);
        long last;
        long i = 1;
        long arc = GetSunLongitudeIndex(GetNewMoonDay(k + i, timeZone), timeZone);
        do
        {
            last = arc;
            i++;
            arc = GetSunLongitudeIndex(GetNewMoonDay(k + i, timeZone), timeZone);
        } while (arc != last && i < 14);
        return i - 1;
    }

    /// <summary>Đổi ngày dương → âm lịch VN. Trả (ngày, tháng, năm âm, có phải tháng nhuận).</summary>
    public static (int Day, int Month, int Year, bool IsLeapMonth) Solar2Lunar(int dd, int mm, int yy, double timeZone = TimeZone)
    {
        long dayNumber = JdFromDate(dd, mm, yy);
        long k = (long)Math.Floor((dayNumber - JdNewMoonEpoch) / NewMoonCycle);
        long monthStart = GetNewMoonDay(k + 1, timeZone);
        if (monthStart > dayNumber) monthStart = GetNewMoonDay(k, timeZone);

        long a11 = GetLunarMonth11(yy, timeZone);
        long b11 = a11;
        int lunarYear;
        if (a11 >= monthStart)
        {
            lunarYear = yy;
            a11 = GetLunarMonth11(yy - 1, timeZone);
        }
        else
        {
            lunarYear = yy + 1;
            b11 = GetLunarMonth11(yy + 1, timeZone);
        }

        int lunarDay = (int)(dayNumber - monthStart + 1);
        long diff = (long)Math.Floor((monthStart - a11) / 29.0);
        bool lunarLeap = false;
        long lunarMonth = diff + 11;
        if (b11 - a11 > 365)
        {
            long leapMonthDiff = GetLeapMonthOffset(a11, timeZone);
            if (diff >= leapMonthDiff)
            {
                lunarMonth = diff + 10;
                if (diff == leapMonthDiff) lunarLeap = true;
            }
        }
        if (lunarMonth > 12) lunarMonth -= 12;
        if (lunarMonth >= 11 && diff < 4) lunarYear -= 1;

        return (lunarDay, (int)lunarMonth, lunarYear, lunarLeap);
    }

    /// <summary>Đổi ngày âm → dương lịch VN. Trả (0,0,0) nếu input nhuận không hợp lệ.</summary>
    public static (int Day, int Month, int Year) Lunar2Solar(int lunarDay, int lunarMonth, int lunarYear, bool isLeapMonth = false, double timeZone = TimeZone)
    {
        long a11, b11;
        if (lunarMonth < 11)
        {
            a11 = GetLunarMonth11(lunarYear - 1, timeZone);
            b11 = GetLunarMonth11(lunarYear, timeZone);
        }
        else
        {
            a11 = GetLunarMonth11(lunarYear, timeZone);
            b11 = GetLunarMonth11(lunarYear + 1, timeZone);
        }

        long k = (long)Math.Floor(0.5 + (a11 - JdNewMoonEpoch) / NewMoonCycle);
        long off = lunarMonth - 11;
        if (off < 0) off += 12;
        if (b11 - a11 > 365)
        {
            long leapOff = GetLeapMonthOffset(a11, timeZone);
            long leapMonth = leapOff - 2;
            if (leapMonth < 0) leapMonth += 12;
            if (isLeapMonth && lunarMonth != leapMonth) return (0, 0, 0);
            if (isLeapMonth || off >= leapOff) off += 1;
        }
        long monthStart = GetNewMoonDay(k + off, timeZone);
        return JdToDate(monthStart + lunarDay - 1);
    }
}
