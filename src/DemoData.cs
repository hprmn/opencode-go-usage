using System;
using System.Globalization;
using System.Text;

namespace OpenCodeUsage
{
    internal static class DemoData
    {
        /// <summary>Sample payload with the documented shape, using live-looking reset times.</summary>
        public static string Build()
        {
            DateTime now = DateTime.UtcNow;
            string rolling = Iso(now.AddHours(4).AddMinutes(12));
            string weekly = Iso(now.AddDays(2).AddHours(5));
            string monthly = Iso(now.AddDays(12).AddHours(3));

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"usage\": {\n");
            sb.Append("    \"rolling\": { \"status\": \"ok\", \"percent\": 12, \"resetsAt\": \"").Append(rolling).Append("\" },\n");
            sb.Append("    \"weekly\": { \"status\": \"ok\", \"percent\": 78, \"resetsAt\": \"").Append(weekly).Append("\" },\n");
            sb.Append("    \"monthly\": { \"status\": \"ok\", \"percent\": 94, \"resetsAt\": \"").Append(monthly).Append("\" }\n");
            sb.Append("  },\n");
            sb.Append("  \"plan\": \"go\",\n");
            sb.Append("  \"workspace\": \"Personal\"\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string Iso(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }
    }
}
