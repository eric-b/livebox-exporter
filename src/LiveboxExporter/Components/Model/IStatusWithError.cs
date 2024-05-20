namespace LiveboxExporter.Components.Model
{
    public interface IWithError
    {
        ResultError[]? Errors { get; set; }
    }
}
