using System.Text.RegularExpressions;

namespace NikonCamera.Tools.Misc;

public static class Barcode
{
    private const string PatternArianaLabTagebuchnummer = @"B\d\d-\d\d\d\d\d\d(-\d\d)?";

    public static bool IsArianaLabBarcode(string barcode)
    {
        var match = Regex.Match(barcode, PatternArianaLabTagebuchnummer);

        return match.Success;
    }
}