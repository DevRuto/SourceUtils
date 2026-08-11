using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using ImageMagick;
using Newtonsoft.Json;
using OpenTK.Graphics.ES20;

namespace SourceUtils.MapExport
{
    public class TextureParameters
    {
        [JsonProperty( "wrapS" )]
        public TextureWrapMode? WrapS { get; set; }

        [JsonProperty( "wrapT" )]
        public TextureWrapMode? WrapT { get; set; }

        [JsonProperty( "filter" )]
        public TextureMinFilter? Filter { get; set; }

        [JsonProperty( "mipmap" )]
        public bool? MipMap { get; set; }
    }

    public class TextureElement
    {
        [JsonProperty( "level" )]
        public int Level { get; set; }

        [JsonProperty( "frame" )]
        public int? Frame { get; set; }

        [JsonProperty( "target" )]
        public TextureTarget? Target { get; set; }

        [JsonProperty( "url" )]
        public Url? Url { get; set; }

        [JsonProperty( "color" )]
        public MaterialColor? Color { get; set; }
    }

    public partial class Texture
    {
        /// <summary>
        /// Parses the texture path back out of a "/materials/.../foo.vtf.json" (or
        /// "/maps/{map}/materials/...") URL produced by <see cref="MaterialService.GetTextureUrl"/>.
        /// Ported from SourceUtils.WebExport.TextureController.GetTexturePath.
        /// </summary>
        public static string GetTexturePathFromUrl( string urlPath )
        {
            var path = urlPath.Substring( urlPath.IndexOf( "/materials" ) + 1 );
            return WebUtility.UrlDecode( path.Substring( 0, path.Length - ".json".Length ) );
        }

        private static readonly Regex _sImageFileNameRegex = new Regex(
            @"^((?<param>mip|face|frame)(?<value>[0-9]+)\.)*(?<format>png)$", RegexOptions.IgnoreCase | RegexOptions.Compiled );

        /// <summary>
        /// Writes the decoded PNG for a "/materials/.../foo.vtf/mip0.frame0.png"-style URL (as
        /// referenced by <see cref="TextureElement.Url"/>). Ported from
        /// SourceUtils.WebExport.TextureController.GetImage.
        /// </summary>
        public static void WriteImage( ValveBspFile bsp, string urlPath, Stream output )
        {
            var pathStart = urlPath.IndexOf( "/materials" ) + 1;
            var pathEnd = urlPath.IndexOf( ".vtf", pathStart ) + ".vtf".Length;
            var path = WebUtility.UrlDecode( urlPath.Substring( pathStart, pathEnd - pathStart ) );

            var fileName = System.IO.Path.GetFileName( urlPath );
            var match = _sImageFileNameRegex.Match( fileName );

            if ( !match.Success ) throw new FileNotFoundException( "File not found.", urlPath );

            var mip = 0;
            var face = 0;
            var frame = 0;

            var index = 0;
            foreach ( Capture capture in match.Groups["param"].Captures )
            {
                var param = capture.Value.ToLower();
                var value = int.Parse( match.Groups["value"].Captures[index++].Value );

                switch ( param )
                {
                    case "mip":
                        mip = value;
                        break;
                    case "face":
                        face = value;
                        break;
                    case "frame":
                        frame = value;
                        break;
                }
            }

            var res = bsp == null ? ExportContext.Resources : bsp.PakFile;

            ValveTextureFile vtf;
            using ( var stream = res.OpenFile( path ) )
            {
                vtf = new ValveTextureFile( stream );
            }

            using ( var image = DecodeImage( vtf, mip, frame, face, 0 ) )
            {
                image.Write( output, MagickFormat.Png );
            }
        }

        public static Texture Get( ValveBspFile bsp, string path )
        {
            var inBsp = bsp != null && bsp.PakFile.ContainsFile( path );
            var res = inBsp ? bsp.PakFile : ExportContext.Resources;

            if ( !res.ContainsFile( path ) ) return null;

            ValveTextureFile vtf;
            using ( var stream = res.OpenFile( path ) )
            {
                vtf = new ValveTextureFile( stream );
            }

            var isCube = vtf.FaceCount == 6;
            var untextured = ExportContext.Untextured && !path.StartsWith( "materials/skybox/" );

            var tex = new Texture
            {
                Path = path.ToLower(),
                Target = isCube ? TextureTarget.TextureCubeMap : TextureTarget.Texture2D,
                Width = untextured ? 1 : vtf.Header.Width,
                Height = untextured ? 1 : vtf.Header.Height,
                FrameCount = vtf.Header.Frames,
                Params =
                {
                    Filter = untextured || (vtf.Header.Flags & TextureFlags.POINTSAMPLE) != 0
                        ? TextureMinFilter.Nearest
                        : TextureMinFilter.Linear,
                    MipMap = !untextured && (vtf.Header.Flags & TextureFlags.NOMIP) == 0,
                    WrapS = (vtf.Header.Flags & TextureFlags.CLAMPS) != 0
                        ? TextureWrapMode.ClampToEdge
                        : TextureWrapMode.Repeat,
                    WrapT = (vtf.Header.Flags & TextureFlags.CLAMPT) != 0
                        ? TextureWrapMode.ClampToEdge
                        : TextureWrapMode.Repeat
                }
            };

            byte[] pixelBuf = null;

            for ( var mip = vtf.MipmapCount - 1; mip >= 0; --mip )
            {
                var isSinglePixel = Math.Max( tex.Width >> mip, 1 ) * Math.Max( tex.Height >> mip, 1 ) == 1;

                for ( var frame = 0; frame < vtf.FrameCount; ++frame )
                {
                    for ( var face = 0; face < vtf.FaceCount; ++face )
                    {
                        var elem = new TextureElement
                        {
                            Level = untextured ? 0 : mip,
                            Frame = vtf.FrameCount > 1 ? (int?) frame : null
                        };

                        if ( isCube ) elem.Target = TextureTarget.TextureCubeMapPositiveX + face;
                        if ( isSinglePixel )
                        {
                            if ( pixelBuf == null ) pixelBuf = new byte[vtf.GetHiResPixelDataLength( mip )];

                            vtf.GetHiResPixelData( mip, frame, face, 0, pixelBuf );

                            using ( var img = DecodeImage( vtf, mip, frame, face, 0 ) )
                            {
                                var pixel = img.GetPixels()[0, 0];
                                switch ( pixel.Channels )
                                {
                                    case 1:
                                        elem.Color = new MaterialColor( pixel[0], pixel[0], pixel[0] );
                                        break;
                                    case 3:
                                        elem.Color = new MaterialColor( pixel[0], pixel[1], pixel[2] );
                                        break;
                                    case 4:
                                        elem.Color = new MaterialColor( pixel[0], pixel[1], pixel[2], pixel[3] );
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                        }
                        else
                        {
                            var fileName = $"{path}/mip{mip}";

                            if ( vtf.FrameCount > 1 )
                            {
                                fileName = $"{fileName}.frame{frame}";
                            }

                            if ( vtf.FaceCount > 1 )
                            {
                                fileName = $"{fileName}.face{face}";
                            }

                            fileName = $"{fileName}.png";

                            elem.Url = inBsp ? $"/maps/{bsp.Name}/{fileName}" : $"/{fileName}";
                        }

                        tex.Elements.Add( elem );
                    }
                }

                if ( untextured ) break;
            }

            return tex;
        }

        /// <remarks>
        /// Not a SourceUtils.MapExport.Url so that it doesn't trigger a crawl.
        /// </remarks>
        [JsonProperty( "path" )]
        public string Path { get; set; }

        [JsonProperty( "target" )]
        public TextureTarget Target { get; set; }

        [JsonProperty( "frames" )]
        public int FrameCount { get; set; } = 1;

        [JsonProperty( "width" )]
        public int Width { get; set; }

        [JsonProperty( "height" )]
        public int Height { get; set; }

        [JsonProperty( "params" )]
        public TextureParameters Params { get; } = new TextureParameters();

        [JsonProperty( "elements" )]
        public List<TextureElement> Elements { get; } = new List<TextureElement>();
    }
}
