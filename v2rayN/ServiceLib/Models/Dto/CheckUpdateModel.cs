namespace ServiceLib.Models.Dto;

public partial class CheckUpdateModel : ReactiveObject
{
    public bool? IsSelected { get; set; }
    public bool IsCheckPreRelease { get; set; }
    public bool ShowCheckPreRelease { get; set; }
    public ECoreType? CoreType { get; set; }
    [Reactive] public partial string? Remarks { get; set; }
    public string Installed { get; set; } = "—";
    public string DisplayName => CoreType == ECoreType.sing_box ? "sing-box" : IsGeoFile ? "Geo" : CoreType.ToString();
    public UpdateResult? Release { get; set; }
    public bool Available { get; set; }
    public bool CheckFailed { get; set; }
    public bool IsGeoFile { get; set; }
    public string CoreTypeForStorage => IsGeoFile ? "GeoFiles" : (CoreType?.ToString() ?? "");
}
