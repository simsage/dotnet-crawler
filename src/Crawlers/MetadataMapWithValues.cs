namespace Crawlers;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class MetadataMapWithValues
{
    [JsonPropertyName("metadataMap")]
    public MetadataMapping MetadataMap { get; set; } = new MetadataMapping("", "", "");
    [JsonPropertyName("stringValueList")]
    public List<string> StringValueList { get; set; } = [];  // a string values
    [JsonPropertyName("numberValueList")]
    public List<double> NumberValueList { get; set; } = [];  // or double values
    private static readonly double Tolerance = 0.001;

    public MetadataMapWithValues() { }

    public MetadataMapWithValues(MetadataMapping metadataMap)
    {
        MetadataMap.ExtMetadata = metadataMap.ExtMetadata;
        MetadataMap.Metadata = metadataMap.Metadata;
        MetadataMap.Display = metadataMap.Display;
    }

    public override string ToString()
    {
        return StringValueList.Count != 0
            ? $"{MetadataMap}, stringValueList.size={StringValueList.Count}"
            : $"{MetadataMap}, numberValueList.size={NumberValueList.Count}";
    }

    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj?.GetType() != GetType())
            return false;
        if (obj is not MetadataMapWithValues o) return false;
        return o.MetadataMap.ExtMetadata == this.MetadataMap.ExtMetadata &&
               o.MetadataMap.Display == this.MetadataMap.Display &&
               o.MetadataMap.Metadata == this.MetadataMap.Metadata &&
               EqualNumList(o.NumberValueList, this.NumberValueList) &&
               EqualStrList(o.StringValueList, this.StringValueList);
    }

    private static bool EqualStrList(List<string> sl1, List<string> sl2)
    {
        if (sl1.Count == sl2.Count)
        {
            sl1.Sort();
            sl2.Sort();
            for (var i = 0; i < sl1.Count; i++)
            {
                if (sl1[i] != sl2[i])
                    return false;
            }
            return true;
        }
        return false;
    }

    private static bool EqualNumList(List<double> nl1, List<double> nl2)
    {
        if (nl2.Count == nl1.Count)
        {
            nl1.Sort();
            nl2.Sort();
            for (var i = 0; i < nl1.Count; i++)
            {
                if (Math.Abs(nl1[i] - nl2[i]) > Tolerance)
                    return false;
            }
            return true;
        }
        return false;
    }

    // create a copy of the object without settings its string or numeric data
    public MetadataMapWithValues Copy()
    {
        return new MetadataMapWithValues(this.MetadataMap);
    }
}

