using System;
using MySql.Data.MySqlClient;

namespace AAEmu.Commons.Utils.DB;

public static class MySqlDateTimeExtensions
{
    private const int MinSupportedMySqlYear = 1000;

    public static DateTime GetDateTimeOrMinValue(this MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return GetDateTimeOrMinValue(reader, ordinal);
    }

    public static DateTime GetDateTimeOrMinValue(this MySqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return DateTime.MinValue;

        var value = reader.GetDateTime(ordinal);
        return value.Year < MinSupportedMySqlYear ? DateTime.MinValue : value;
    }

    public static void AddDateTimeOrNull(this MySqlParameterCollection parameters, string parameterName, DateTime value)
    {
        parameters.AddWithValue(parameterName, value.ToDbDateTimeOrNull());
    }

    public static object ToDbDateTimeOrNull(this DateTime value)
    {
        return value.Year < MinSupportedMySqlYear ? DBNull.Value : value;
    }
}
