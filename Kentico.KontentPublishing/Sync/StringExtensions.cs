namespace Kentico.EMS.Kontent.Publishing
{
    static class StringExtensions
    {
        public static string LimitedTo(this string value, int maxChars)
        {
            if (value.Length > maxChars)
            {
                return value.Substring(0, maxChars);
            }
            return value;
        }
    }
}
