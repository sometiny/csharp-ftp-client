
namespace Zhger.Net.Ftp.Vendor
{
    internal static class Utility
    {
        public static string IfEmpty(string source, string ifEmpty) => IsEmpty(source) ? ifEmpty : source;
        public static bool IsEmpty(string source) => source == null || source.Length == 0 || string.IsNullOrWhiteSpace(source);
        public static bool IsNotEmpty(string source) => source != null && source.Length != 0 && !string.IsNullOrWhiteSpace(source);
        public static KeyValuePair<string, string> GetKeyValue(string item)
        {
            return GetKeyValue(item, '=');
        }
        public static KeyValuePair<string, string> GetKeyValue(string item, char splitor)
        {
            int idx = item.IndexOf(splitor);
            if (idx == -1) return new KeyValuePair<string, string>(item, "");

            return new KeyValuePair<string, string>(item.Substring(0, idx), item.Substring(idx + 1));
        }

        public static byte[] ReadAllBytes(this Stream source)
        {
            using MemoryStream output = new();
            source.CopyTo(output);
            return output.ToArray();
        }
        public static void TryClose(this Stream stream)
        {
            try
            {
                stream.Close();
            }
            catch { }
        }
    }
}
