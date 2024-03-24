namespace FateAnotherJassReplacement.Utility
{
    public static class NewLineHelper
    {
        public static string ReplaceToUnixNewLine(this string str)
        {
            return str.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
