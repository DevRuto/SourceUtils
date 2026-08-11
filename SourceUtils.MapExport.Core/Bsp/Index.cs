using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SourceUtils.ValveBsp;
using SourceUtils.ValveBsp.Entities;

namespace SourceUtils.MapExport.Bsp
{
    public class PageInfo
    {
        [JsonProperty( "first" )]
        public int First { get; set; }

        [JsonProperty( "count" )]
        public int Count { get; set; }

        [JsonProperty( "url" )]
        public Url Url { get; set; }
    }

    /// <summary>
    /// Ported from SourceUtils.WebExport.Bsp.IndexController, minus the HTTP layer (the
    /// index.html templating and entities.txt debug endpoint aren't part of the map-data export).
    /// </summary>
    public static partial class IndexService
    {
        private static int DefaultItemSizeSelect( int index )
        {
            return 1;
        }

        private static PageInfo CreatePage( ValveBspFile bsp, int first, int last, ref int index, string filePrefix )
        {
            return new PageInfo
            {
                First = first,
                Count = last - first,
                Url = filePrefix != null ? $"/maps/{bsp.Name}{filePrefix}{index++}.json" : null
            };
        }

        public static IEnumerable<PageInfo> GetPageLayout( ValveBspFile bsp, int count, int perPage, string filePrefix, Func<int, int> itemSizeSelect = null )
        {
            if ( itemSizeSelect == null ) itemSizeSelect = DefaultItemSizeSelect;

            var first = 0;
            var size = 0;
            var index = 0;

            for ( var i = 0; i < count; ++i )
            {
                var itemSize = itemSizeSelect( i );

                if ( size + itemSize > perPage && size > 0 )
                {
                    yield return CreatePage( bsp, first, i, ref index, filePrefix );

                    size = 0;
                    first = i;
                }

                size += itemSize;
            }

            yield return CreatePage( bsp, first, count, ref index, filePrefix );
        }

        public class Map
        {
            [JsonProperty( "name" )]
            public string Name { get; set; }

            [JsonProperty( "lightmapUrl" )]
            public Url LightmapUrl { get; set; }

            [JsonProperty( "leafPages" )]
            public IEnumerable<PageInfo> LeafPages { get; set; }

            [JsonProperty( "dispPages" )]
            public IEnumerable<PageInfo> DispPages { get; set; }

            [JsonProperty( "materialPages" )]
            public IEnumerable<PageInfo> MaterialPages { get; set; }

            [JsonProperty( "brushModelPages" )]
            public IEnumerable<PageInfo> BrushModelPages { get; set; }

            [JsonProperty( "studioModelPages" )]
            public IEnumerable<PageInfo> StudioModelPages { get; set; }

            [JsonProperty( "vertLightingPages" )]
            public IEnumerable<PageInfo> VertexLightingPages { get; set; }

            [JsonProperty( "visPages" )]
            public IEnumerable<PageInfo> VisPages { get; set; }

            [JsonProperty( "ambientPages" )]
            public IEnumerable<PageInfo> AmbientPages { get; set; }

            [JsonProperty( "entities" )]
            public IEnumerable<Entity> Entities { get; set; }
        }

        public static Map GetIndexJson( ValveBspFile bsp )
        {
            var ents = new List<Entity>();
            var mapParams = new MapParams( bsp );

            foreach ( var ent in bsp.Entities )
            {
                var inst = InitEntity( ent, mapParams );
                if ( inst != null ) ents.Add( inst );
            }

            for ( var dispIndex = 0; dispIndex < bsp.DisplacementInfos.Length; ++dispIndex )
            {
                SourceUtils.Vector3 min, max;
                GetDisplacementBounds( bsp, dispIndex, out min, out max, 1f );

                ents.Add( new Displacement
                {
                    ClassName = "displacement",
                    Index = dispIndex,
                    Clusters = GetIntersectingClusters( mapParams.Tree, min, max )
                } );
            }

            for ( var propIndex = 0; propIndex < bsp.StaticProps.PropCount; ++propIndex )
            {
                SourceUtils.Vector3 origin, angles;
                float scale;
                bsp.StaticProps.GetPropTransform( propIndex, out origin, out angles, out scale );

                StaticPropFlags flags;
                bool solid;
                uint diffuseMod;
                bsp.StaticProps.GetPropInfo( propIndex, out flags, out solid, out diffuseMod );

                int propModelIndex, skin;
                bsp.StaticProps.GetPropModelSkin( propIndex, out propModelIndex, out skin );

                var modelName = bsp.StaticProps.GetModelName( propModelIndex );
                var modelIndex = StudioModelDictionary.GetResourceIndex( bsp, modelName );

                ents.Add( new StaticProp
                {
                    ClassName = "prop_static",
                    Origin = origin,
                    Angles = angles,
                    Scale = Math.Abs( scale - 1f ) < 0.001f ? null : (float?) scale,
                    Flags = flags,
                    Clusters = bsp.StaticProps.GetPropLeaves( propIndex )
                        .Select( x => (int) bsp.Leaves[x].Cluster )
                        .Where( x => x != -1 )
                        .Distinct(),
                    Model = modelIndex,
                    VertLighting = (flags & StaticPropFlags.NoPerVertexLighting) == 0 ? (int?) propIndex : null,
                    LightingOrigin = (flags & StaticPropFlags.UseLightingOrigin) == 0 ? null : (Vector3?) bsp.StaticProps.GetLightingOrigin( propIndex ),
                    AlbedoModulation = diffuseMod != 0xffffffff ? (uint?) diffuseMod : null
                } );
            }

            return new Map
            {
                Name = bsp.Name,
                LightmapUrl = $"/maps/{bsp.Name}/lightmap.json",
                VisPages = GetPageLayout( bsp, bsp.Visibility.NumClusters, VisPage.ClustersPerPage, "/geom/vispage" ),
                AmbientPages = GetPageLayout( bsp, bsp.Leaves.Length, AmbientPage.LeavesPerPage, "/geom/ambientpage" ),
                LeafPages = GetPageLayout( bsp, bsp.Leaves.Length, LeafGeometryPage.LeavesPerPage, "/geom/leafpage" ),
                DispPages = GetPageLayout( bsp, bsp.DisplacementInfos.Length, DispGeometryPage.DisplacementsPerPage, "/geom/disppage" ),
                MaterialPages = GetPageLayout( bsp, MaterialDictionary.GetResourceCount( bsp ), MaterialPage.MaterialsPerPage, "/materials/matpage" ),
                BrushModelPages = GetPageLayout( bsp, bsp.Models.Length, BspModelPage.FacesPerPage, "/geom/bsppage", i => bsp.Models[i].NumFaces ),
                StudioModelPages = GetPageLayout( bsp, StudioModelDictionary.GetResourceCount( bsp ), StudioModelPage.VerticesPerPage, "/geom/mdlpage", i => StudioModelDictionary.GetVertexCount( bsp, i ) ),
                VertexLightingPages = GetPageLayout( bsp, bsp.StaticProps.PropCount, VertexLightingPage.PropsPerPage, "/geom/vhvpage" ),
                Entities = ents
            };
        }
    }
}
