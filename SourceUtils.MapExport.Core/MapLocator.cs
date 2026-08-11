using System.Collections.Generic;
using System.IO;
using SourceUtils.ValveBsp;

namespace SourceUtils.MapExport
{
    /// <summary>
    /// Resolves map names to .bsp files under a maps directory (including Steam Workshop
    /// subdirectories) and caches the opened <see cref="ValveBspFile"/> instances. Ported from
    /// the map-resolution half of SourceUtils.WebExport.Program (GetMap/UnloadMap/FindWorkshopMaps).
    /// </summary>
    public sealed class MapLocator
    {
        private readonly string _mapsDir;
        private readonly Dictionary<string, ValveBspFile> _openMaps = new Dictionary<string, ValveBspFile>();
        private Dictionary<string, string> _workshopMaps;

        public MapLocator( string mapsDir )
        {
            _mapsDir = mapsDir;
        }

        private void FindWorkshopMaps()
        {
            _workshopMaps = new Dictionary<string, string>();

            var workshopDir = Path.Combine( _mapsDir, "workshop" );

            if ( !Directory.Exists( workshopDir ) ) return;

            foreach ( var directory in Directory.GetDirectories( workshopDir ) )
            {
                if ( !ulong.TryParse( Path.GetFileName( directory ), out _ ) ) continue;

                foreach ( var bsp in Directory.GetFiles( directory, "*.bsp", SearchOption.TopDirectoryOnly ) )
                {
                    var name = Path.GetFileNameWithoutExtension( bsp ).ToLower();
                    if ( !_workshopMaps.ContainsKey( name ) )
                    {
                        _workshopMaps.Add( name, bsp );
                    }
                }
            }
        }

        public ValveBspFile GetMap( string name )
        {
            if ( _openMaps.TryGetValue( name, out var map ) ) return map;

            if ( _workshopMaps == null ) FindWorkshopMaps();

            if ( !_workshopMaps.TryGetValue( name.ToLower(), out var bspPath ) )
            {
                bspPath = Path.Combine( _mapsDir, $"{name}.bsp" );
            }

            map = new ValveBspFile( bspPath );
            _openMaps.Add( name, map );

            return map;
        }

        public void UnloadMap( string name )
        {
            if ( !_openMaps.TryGetValue( name, out var map ) ) return;

            _openMaps.Remove( name );
            map.Dispose();
        }

        public IEnumerable<string> ExpandMapPattern( string pattern )
        {
            var bspPattern = pattern.ToLower().EndsWith( ".bsp" ) ? pattern : $"{pattern}.bsp";

            foreach ( var map in Directory.EnumerateFiles( _mapsDir, bspPattern, SearchOption.TopDirectoryOnly ) )
            {
                yield return Path.GetFileNameWithoutExtension( map );
            }
        }
    }
}
