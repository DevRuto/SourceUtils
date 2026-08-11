using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SourceUtils.ValveBsp;

namespace SourceUtils.MapExport.Bsp
{
    public class VisPage
    {
        public const int ClustersPerPage = 8192;

        [JsonProperty( "values" )]
        public IEnumerable<CompressedList<int>> Values { get; set; }
    }

    /// <summary>Ported from SourceUtils.WebExport.Bsp.VisController, minus the HTTP layer.</summary>
    public static class VisibilityService
    {
        public static VisPage GetPage( ValveBspFile bsp, int page, bool skip = false )
        {
            if ( skip ) return null;

            var first = page * VisPage.ClustersPerPage;
            var count = Math.Min( first + VisPage.ClustersPerPage, bsp.Visibility.NumClusters ) - first;

            if ( count < 0 )
            {
                first = bsp.Visibility.NumClusters;
                count = 0;
            }

            return new VisPage
            {
                Values = Enumerable.Range( first, count ).Select( x => new CompressedList<int>( bsp.Visibility[x] ) )
            };
        }
    }
}
