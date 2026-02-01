using System;
using System.Globalization;
using System.Text;

namespace Web.Api.OtpTemplate;

/// <summary>
/// NEW OTP Builder - Use this one for horizontal layout!
/// </summary>
public static class OtpEmailBuilder
{
    /// <summary>
    /// Builds horizontal OTP boxes for email
    /// </summary>
    /// <param name="otp">The OTP code (e.g., "1822")</param>
    /// <returns>HTML table cells - each digit in separate TD</returns>
    public static string BuildHorizontalOtpBoxes(string otp)
    {
        if (string.IsNullOrWhiteSpace(otp))
        {
            throw new ArgumentException("OTP cannot be null or empty", nameof(otp));
        }

        var html = new StringBuilder();

        for (int i = 0; i < otp.Length; i++)
        {
            // EACH DIGIT IN ITS OWN <td> TAG - THIS IS THE KEY!
            html.AppendFormat(
                CultureInfo.InvariantCulture,
                "<td style=\"width:45px;height:55px;background-color:#f8f8f8;border-radius:8px;text-align:center;vertical-align:middle;font-size:26px;font-weight:700;color:#1a1a1a;\">{0}</td>",
                otp[i]);

            // Add spacing between digits (not after last digit)
            if (i < otp.Length - 1)
            {
                html.Append("<td style=\"width:8px;\"></td>");
            }
        }

        return html.ToString();
    }
}
