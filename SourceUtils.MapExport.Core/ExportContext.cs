namespace SourceUtils.MapExport
{
    /// <summary>
    /// Process-wide settings consulted by the ported map-data services, mirroring the subset of
    /// SourceUtils.WebExport.Program's static state (Resources/BaseOptions) that the original
    /// controllers relied on. Set once by the driving console app before exporting.
    /// </summary>
    public static class ExportContext
    {
        public static IResourceProvider Resources { get; set; }
        public static bool Untextured { get; set; }
        public static bool DebugMaterials { get; set; }
    }
}
