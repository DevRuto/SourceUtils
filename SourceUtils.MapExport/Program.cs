using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CommandLine;
using Newtonsoft.Json.Linq;
using SourceUtils;
using SourceUtils.MapExport;
using SourceUtils.MapExport.Bsp;

namespace SourceUtils.MapExport.Console
{
    class Options
    {
        [Option( 'g', "gamedir", HelpText = "Game directory to export from.", Required = true )]
        public string GameDir { get; set; }

        [Option( 'l', "loosedir", HelpText = "(Additional) directory to load loose files from." )]
        public string LooseDir { get; set; }

        [Option( "packages", HelpText = "Comma separated VPK file names." )]
        public string Packages { get; set; } = "pak01_dir.vpk";

        [Option( 'm', "mapsdir", HelpText = "Directory to export maps from, relative to gamedir." )]
        public string MapsDir { get; set; } = "maps";

        [Option( "maps", HelpText = "Specific comma separated map names to export (e.g. 'de_dust2,de_mirage,kz_*').", Required = true )]
        public string Maps { get; set; }

        [Option( 'o', "outdir", HelpText = "Output directory.", Required = true )]
        public string OutDir { get; set; }

        [Option( 'r', "overwrite", HelpText = "Overwrite existing exported files." )]
        public bool Overwrite { get; set; }

        [Option( 'v', "verbose", HelpText = "Write every action to standard output." )]
        public bool Verbose { get; set; }

        [Option( "untextured", HelpText = "Only export a single colour for each texture." )]
        public bool Untextured { get; set; }

        [Option( "debug-materials", HelpText = "Include all material properties." )]
        public bool DebugMaterials { get; set; }

        [Option( "debug-pakfile", HelpText = "Save pakfile to disk for each map, for debugging." )]
        public bool DebugPakFile { get; set; }

        [Option( "dry", HelpText = "Don't actually write any files, just test exporting." )]
        public bool DryRun { get; set; }
    }

    /// <summary>
    /// Exports a map's BSP-derived JSON/PNG data (geometry, materials, textures, lightmap,
    /// visibility, entities) directly to disk, without ever starting an HTTP server. Unlike
    /// SourceUtils.WebExport's `export` verb - which drives the same kind of output by spinning
    /// up a loopback HttpListener and crawling it with HttpClient - this calls straight into
    /// SourceUtils.MapExport.Core's services and writes files in-process. It does not produce the
    /// static viewer shell (index.html/js/css/config.json); it's meant for callers that already
    /// have their own viewer and just need the map data.
    /// </summary>
    static class Program
    {
        private static readonly Regex PageUrlRegex = new Regex(
            @"^/maps/(?<map>[^/]+)/geom/(?<kind>leafpage|disppage|bsppage|mdlpage|vhvpage|vispage|ambientpage)(?<index>\d+)\.json$",
            RegexOptions.Compiled );

        private static readonly Regex MaterialPageUrlRegex = new Regex(
            @"^/maps/(?<map>[^/]+)/materials/matpage(?<index>\d+)\.json$", RegexOptions.Compiled );

        static int Main( string[] args )
        {
            var culture = new CultureInfo( "en-US" );
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            var result = Parser.Default.ParseArguments<Options>( args );
            return result.MapResult( Run, _ => 1 );
        }

        static int Run( Options args )
        {
            var vpkNames = args.Packages.Split( new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( x => Path.IsPathRooted( x ) ? x.Trim() : Path.Combine( args.GameDir, x.Trim() ) )
                .ToArray();

            var loader = new ResourceLoader();

            if ( !string.IsNullOrEmpty( args.LooseDir ) )
            {
                loader.AddResourceProvider( new FSLoader( args.LooseDir ) );
            }

            foreach ( var path in vpkNames )
            {
                loader.AddResourceProvider( new ValvePackage( path ) );
            }

            ExportContext.Resources = loader;
            ExportContext.Untextured = args.Untextured;
            ExportContext.DebugMaterials = args.DebugMaterials;

            ValveBspFile.PakFileLump.DebugContents = args.DebugPakFile;

            var mapsDir = string.IsNullOrEmpty( args.MapsDir ) ? "maps" : args.MapsDir;
            if ( !Path.IsPathRooted( mapsDir ) ) mapsDir = Path.Combine( args.GameDir, mapsDir );

            var locator = new MapLocator( mapsDir );

            var items = args.Maps.Split( new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries );

            foreach ( var item in items )
            {
                if ( item.Contains( "*" ) )
                {
                    foreach ( var map in locator.ExpandMapPattern( item ) )
                    {
                        ExportMap( map, locator, args );
                    }
                }
                else
                {
                    var map = item.ToLower().EndsWith( ".bsp" )
                        ? item.Substring( 0, item.Length - ".bsp".Length )
                        : item;

                    ExportMap( map, locator, args );
                }
            }

            return 0;
        }

        static void ExportMap( string mapName, MapLocator locator, Options args )
        {
            var exported = 0;
            var skipped = 0;
            var failed = 0;

            System.Console.ResetColor();
            System.Console.WriteLine();
            System.Console.WriteLine( $"# Exporting {mapName}" );
            System.Console.WriteLine();

            var bsp = locator.GetMap( mapName );

            var seen = new HashSet<string>();
            var queue = new Queue<string>();

            void Enqueue( Url url )
            {
                if ( !url.Export ) return;
                if ( seen.Add( url.Value ) ) queue.Enqueue( url.Value );
            }

            var indexUrl = $"/maps/{mapName}/index.json";
            seen.Add( indexUrl );
            queue.Enqueue( indexUrl );

            MemoryStream dummyStream = args.DryRun ? new MemoryStream() : null;

            var total = 1;

            while ( queue.Count > 0 )
            {
                var url = queue.Dequeue();

                var path = Path.Combine( args.OutDir, url.Substring( 1 ) );
                var skip = !args.Overwrite && File.Exists( path );

                if ( skip ) ++skipped;

                if ( args.Verbose )
                {
                    System.Console.ResetColor();
                    if ( skip ) System.Console.WriteLine( $"Skipped '{url}'" );
                    else System.Console.Write( $"[{exported + skipped + failed + 1}/{total}] Exporting '{url}' ... " );
                }

                try
                {
                    using ( var buffer = new MemoryStream() )
                    {
                        bool hasContent;
                        using ( UrlCrawl.Begin( Enqueue ) )
                        {
                            hasContent = Produce( bsp, mapName, url, skip, buffer );
                        }

                        total = seen.Count;

                        if ( !hasContent || skip )
                        {
                            if ( args.Verbose && !skip ) System.Console.WriteLine( "(empty)" );
                            continue;
                        }

                        var dir = Path.GetDirectoryName( path );
                        if ( !args.DryRun && dir != null && !Directory.Exists( dir ) )
                        {
                            Directory.CreateDirectory( dir );
                        }

                        long length;

                        buffer.Seek( 0, SeekOrigin.Begin );

                        if ( !args.DryRun )
                        {
                            using ( var output = File.Create( path ) )
                            {
                                buffer.CopyTo( output );
                                length = output.Length;
                            }
                        }
                        else
                        {
                            dummyStream.Seek( 0, SeekOrigin.Begin );
                            dummyStream.SetLength( 0 );
                            buffer.CopyTo( dummyStream );
                            length = dummyStream.Length;
                        }

                        ++exported;

                        if ( args.Verbose )
                        {
                            System.Console.ForegroundColor = ConsoleColor.Green;
                            System.Console.WriteLine( $"Wrote {length} Bytes" );
                        }
                    }
                }
                catch ( Exception e )
                {
                    ++failed;

                    if ( args.Verbose )
                    {
                        System.Console.ForegroundColor = ConsoleColor.DarkRed;
                        System.Console.WriteLine( "Failed" );
                        System.Console.WriteLine( e );
                    }
                }
            }

            locator.UnloadMap( mapName );

            System.Console.ResetColor();
            System.Console.WriteLine();
            System.Console.WriteLine( $"# Finished {mapName} ({exported} exported, {skipped} skipped, {failed} failed)" );
            System.Console.WriteLine();
        }

        private static void WriteJson( object value, Stream output )
        {
            if ( value == null ) return;

            var token = MapJsonSerializer.ToJToken( value );
            if ( token == null ) return;

            using ( var writer = new StreamWriter( output, System.Text.Encoding.UTF8, 4096, leaveOpen: true ) )
            {
                writer.Write( token.ToString( Newtonsoft.Json.Formatting.None ) );
            }
        }

        /// <returns>false if the URL didn't map to anything (nothing was written)</returns>
        private static bool Produce( ValveBspFile bsp, string mapName, string url, bool skip, Stream output )
        {
            if ( url == $"/maps/{mapName}/index.json" )
            {
                WriteJson( IndexService.GetIndexJson( bsp ), output );
                return true;
            }

            if ( url == $"/maps/{mapName}/lightmap.json" )
            {
                WriteJson( LightmapService.GetInfo( bsp ), output );
                return true;
            }

            if ( url == $"/maps/{mapName}/lightmap.png" )
            {
                if ( !skip ) LightmapService.WriteImage( bsp, output );
                return true;
            }

            var pageMatch = PageUrlRegex.Match( url );
            if ( pageMatch.Success && pageMatch.Groups["map"].Value == mapName )
            {
                var index = int.Parse( pageMatch.Groups["index"].Value );

                switch ( pageMatch.Groups["kind"].Value )
                {
                    case "leafpage":
                        WriteJson( GeometryService.GetLeafPage( bsp, index, skip ), output );
                        return true;
                    case "disppage":
                        WriteJson( GeometryService.GetDispPage( bsp, index, skip ), output );
                        return true;
                    case "bsppage":
                        WriteJson( BrushModelService.GetPage( bsp, index ), output );
                        return true;
                    case "mdlpage":
                        WriteJson( GeometryService.GetStudioModelPage( bsp, index ), output );
                        return true;
                    case "vhvpage":
                        WriteJson( GeometryService.GetVertexLightingPage( bsp, index ), output );
                        return true;
                    case "vispage":
                        WriteJson( VisibilityService.GetPage( bsp, index, skip ), output );
                        return true;
                    case "ambientpage":
                        WriteJson( AmbientService.GetPage( bsp, index, skip ), output );
                        return true;
                }
            }

            var matPageMatch = MaterialPageUrlRegex.Match( url );
            if ( matPageMatch.Success && matPageMatch.Groups["map"].Value == mapName )
            {
                var index = int.Parse( matPageMatch.Groups["index"].Value );
                WriteJson( MaterialPageService.GetPage( bsp, index ), output );
                return true;
            }

            var mapMaterialsPrefix = $"/maps/{mapName}/materials";
            var isMapScoped = url.StartsWith( mapMaterialsPrefix + "/" );
            var isGlobalMaterials = !isMapScoped && url.StartsWith( "/materials/" );

            if ( isMapScoped || isGlobalMaterials )
            {
                var materialBsp = isMapScoped ? bsp : null;

                if ( url.EndsWith( ".vmt.json" ) )
                {
                    var texPath = Texture.GetTexturePathFromUrl( url );
                    WriteJson( Material.Get( materialBsp, texPath ), output );
                    return true;
                }

                if ( url.EndsWith( ".vtf.json" ) )
                {
                    var texPath = Texture.GetTexturePathFromUrl( url );
                    WriteJson( Texture.Get( materialBsp, texPath ), output );
                    return true;
                }

                if ( url.EndsWith( ".png" ) )
                {
                    if ( !skip ) Texture.WriteImage( materialBsp, url, output );
                    return true;
                }
            }

            return false;
        }
    }
}
