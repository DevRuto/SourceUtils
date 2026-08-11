using System;
using System.Collections.Generic;
using System.Linq;
using SourceUtils.ValveBsp;

namespace SourceUtils.MapExport
{
    public abstract class ResourceDictionary<TDictionary>
        where TDictionary : ResourceDictionary<TDictionary>, new()
    {
        private static readonly Dictionary<ValveBspFile, TDictionary> _sDicts =
            new Dictionary<ValveBspFile, TDictionary>();

        protected static TDictionary GetDictionary( ValveBspFile bsp )
        {
            TDictionary dict;
            if ( _sDicts.TryGetValue( bsp, out dict ) ) return dict;

            dict = new TDictionary();
            dict.FindResourcePaths( bsp );

            bsp.Disposing += _ => _sDicts.Remove( bsp );

            _sDicts.Add( bsp, dict );

            return dict;
        }

        public static int GetResourceCount( ValveBspFile bsp )
        {
            return GetDictionary( bsp ).ResourceCount;
        }

        public static int GetResourceIndex( ValveBspFile bsp, string path )
        {
            return GetDictionary( bsp ).GetResourceIndex( path );
        }

        public static string GetResourcePath( ValveBspFile bsp, int index )
        {
            return GetDictionary( bsp ).GetResourcePath( index );
        }

        private readonly List<string> _resources = new List<string>();

        private readonly Dictionary<string, int> _indices =
            new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase );

        public int ResourceCount => _resources.Count;

        private void FindResourcePaths( ValveBspFile bsp )
        {
            foreach ( var path in OnFindResourcePaths( bsp ) )
            {
                Add( path );
            }
        }

        protected abstract IEnumerable<string> OnFindResourcePaths( ValveBspFile bsp );

        protected virtual string NormalizePath( string path )
        {
            return path.ToLower().Replace( '\\', '/' ).Replace( "//", "/" );
        }

        private void Add( string path )
        {
            path = NormalizePath( path );

            if ( _indices.ContainsKey( path ) ) return;

            _indices.Add( path, _resources.Count );
            _resources.Add( path );
        }

        public string GetResourcePath( int index )
        {
            return _resources[index];
        }

        public int GetResourceIndex( string path )
        {
            path = NormalizePath( path );

            int index;
            if ( _indices.TryGetValue( path, out index ) ) return index;
            return -1;
        }
    }

    public class MaterialDictionary : ResourceDictionary<MaterialDictionary>
    {
        protected override IEnumerable<string> OnFindResourcePaths( ValveBspFile bsp )
        {
            for ( var i = 0; i < bsp.TextureStringTable.Length; ++i )
            {
                yield return bsp.GetTextureString( i );
            }

            for ( var i = 0; i < bsp.StaticProps.ModelCount; ++i )
            {
                var modelName = bsp.StaticProps.GetModelName( i );
                var mdl = StudioModelFile.FromProvider( modelName, bsp.PakFile, ExportContext.Resources );

                if ( mdl == null ) continue;

                for ( var j = 0; j < mdl.MaterialCount; ++j )
                {
                    yield return mdl.GetMaterialName( j, bsp.PakFile, ExportContext.Resources );
                }
            }

            foreach ( var entity in bsp.Entities )
            {
                switch ( entity.ClassName )
                {
                    case "move_rope":
                    case "keyframe_rope":
                        yield return entity["RopeMaterial"];
                        break;
                }
            }
        }

        protected override string NormalizePath( string path )
        {
            path = base.NormalizePath( path );

            if ( !path.StartsWith( "materials/" ) ) path = $"materials/{path}".Replace( "//", "/" );
            if ( !path.EndsWith( ".vmt" ) ) path = $"{path}.vmt";

            return path;
        }
    }

    public class StudioModelDictionary : ResourceDictionary<StudioModelDictionary>
    {
        public static int GetVertexCount( ValveBspFile bsp, int index )
        {
            return GetDictionary( bsp ).GetVertexCount( index );
        }

        private readonly List<int> _vertexCounts = new List<int>();

        protected override IEnumerable<string> OnFindResourcePaths( ValveBspFile bsp )
        {
            var items = Enumerable.Range( 0, bsp.StaticProps.ModelCount )
                .Select( x => bsp.StaticProps.GetModelName( x ) )
                .Select( x =>
                {
                    var mdl = StudioModelFile.FromProvider( x, bsp.PakFile, ExportContext.Resources );
                    if ( mdl == null ) return null;
                    return new
                    {
                        Path = x,
                        VertexCount = mdl.TotalVertices,
                        FirstMaterialIndex = MaterialDictionary.GetResourceIndex( bsp, mdl.GetMaterialName( 0, bsp.PakFile, ExportContext.Resources ) )
                    };
                } )
                .Where( x => x != null )
                .GroupBy( x => x.FirstMaterialIndex )
                .OrderByDescending( x => x.Count() )
                .SelectMany( x => x )
                .ToArray();

            foreach ( var item in items )
            {
                yield return item.Path;

                var index = GetResourceIndex( item.Path );
                if ( index == _vertexCounts.Count )
                {
                    _vertexCounts.Add( item.VertexCount );
                }
            }
        }

        public int GetVertexCount( int index )
        {
            return _vertexCounts[index];
        }

        protected override string NormalizePath( string path )
        {
            path = base.NormalizePath( path );

            if ( !path.StartsWith( "models/" ) ) path = $"models/{path}";
            if ( !path.EndsWith( ".mdl" ) ) path = $"{path}.mdl";

            return path;
        }
    }
}
