using Newtonsoft.Json;
using SourceUtils.WebExport.Hosting;

namespace SourceUtils.WebExport
{
    public class ConfigInfo
    {
        [JsonProperty( "urlPrefix" )]
        public string UrlPrefix { get; set; }
    }

    [Prefix( "" )]
    class ConfigController : ResourceController
    {
        [Get( "/config.json" )]
        public ConfigInfo Get()
        {
            return new ConfigInfo
            {
                UrlPrefix = Program.IsExporting ? Program.ExportOptions.UrlPrefix ?? "" : ""
            };
        }
    }
}
