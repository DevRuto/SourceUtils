using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SourceUtils.ValveBsp;

namespace SourceUtils.MapExport.Bsp
{
    public class AmbientCube
    {
        [JsonProperty( "position" )]
        public Vector3 Position { get; set; }

        [JsonProperty( "samples" )]
        public int[] Samples { get; set; }
    }

    public class AmbientPage
    {
        public const int LeavesPerPage = 4096;

        [JsonProperty( "values" )]
        public IEnumerable<List<AmbientCube>> Values { get; set; }
    }

    /// <summary>Ported from SourceUtils.WebExport.Bsp.AmbientController, minus the HTTP layer.</summary>
    public static class AmbientService
    {
        public static AmbientPage GetPage( ValveBspFile bsp, int page, bool skip = false )
        {
            if ( skip ) return null;

            var first = page * AmbientPage.LeavesPerPage;
            var count = Math.Min( first + AmbientPage.LeavesPerPage, bsp.Leaves.Length ) - first;

            if ( count < 0 )
            {
                first = bsp.Leaves.Length;
                count = 0;
            }

            var hdr = bsp.LeafAmbientLightingHdr.Length > bsp.LeafAmbientLighting.Length;
            var indices = hdr ? bsp.LeafAmbientIndicesHdr : bsp.LeafAmbientIndices;
            var ambients = hdr ? bsp.LeafAmbientLightingHdr : bsp.LeafAmbientLighting;

            return new AmbientPage
            {
                Values = Enumerable.Range( first, count ).Select( x =>
                {
                    var leaf = bsp.Leaves[x];
                    var index = indices[x];
                    var list = new List<AmbientCube>( index.AmbientSampleCount );

                    var min = new SourceUtils.Vector3( leaf.Min.X, leaf.Min.Y, leaf.Min.Z );
                    var max = new SourceUtils.Vector3( leaf.Max.X, leaf.Max.Y, leaf.Max.Z );

                    for ( var i = 0; i < index.AmbientSampleCount; ++i )
                    {
                        var ambient = ambients[index.FirstAmbientSample + i];
                        var samples = new int[6];
                        var relPos = new SourceUtils.Vector3( ambient.X, ambient.Y, ambient.Z ) * (1f / 255f);

                        for ( var j = 0; j < 6; ++j )
                        {
                            samples[j] = ambient.Cube[j];
                        }

                        list.Add( new AmbientCube
                        {
                            Position = (min + relPos * (max - min)).Rounded,
                            Samples = samples
                        } );
                    }

                    return list;
                } )
            };
        }
    }
}
