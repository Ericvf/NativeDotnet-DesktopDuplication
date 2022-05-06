public class Helpers
{
    public static int RoundUp(int numToRound, int multiple)
    {
        return ((numToRound + multiple - 1) / multiple) * multiple;
    }

    public static string GetAssetFullPath(string assetName) => Path.Combine(AppContext.BaseDirectory, assetName);
}
