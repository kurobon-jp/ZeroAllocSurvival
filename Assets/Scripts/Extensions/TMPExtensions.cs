using TMPro;

namespace ZeroAllocSurvival.Extensions
{
    public static class TMPExtensions
    {
        public static void Warmup(this TMP_Text text)
        {
            var initText = text.text;
            const string warmupText = " .0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            text.text = warmupText;
            text.ForceMeshUpdate();
            text.text = initText;
        }
    }
}