using System;
using System.Linq;
using System.Net;
using Newtonsoft.Json;

namespace SourceUtils.MapExport
{
    /// <summary>
    /// A resource path that gets wrapped as <c>{"$url": ...}</c> when serialized, so the viewer
    /// can resolve it at runtime against a deployment's URL prefix. Ported from
    /// SourceUtils.WebExport.ResourceController's Url/UrlConverter, with the export-time crawl
    /// hook (originally driven by the WebExport HTTP loopback) replaced by <see cref="UrlCrawl"/>,
    /// an explicit ambient scope set by the exporter driving this library.
    /// </summary>
    [JsonConverter( typeof(UrlConverter) )]
    public struct Url : IEquatable<Url>
    {
        public static implicit operator Url( string value )
        {
            return new Url( value );
        }

        public static implicit operator string( Url url )
        {
            return url.Value;
        }

        public readonly string Value;
        public readonly bool Export;

        public Url( string value, bool export = true )
        {
            Value = value;
            Export = export;
        }

        public bool Equals( Url other )
        {
            return string.Equals( Value, other.Value );
        }

        public override bool Equals( object obj )
        {
            return obj is Url other && Equals( other );
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    /// <summary>
    /// Ambient scope that receives every <see cref="Url"/> discovered while serializing a JSON
    /// payload (see <see cref="UrlConverter.WriteJson"/>), so a caller can crawl the full set of
    /// files a map export touches without having to separately re-derive resource references
    /// (e.g. which textures a material references).
    /// </summary>
    public static class UrlCrawl
    {
        [ThreadStatic]
        private static Action<Url> _sSink;

        private sealed class Scope : IDisposable
        {
            private readonly Action<Url> _previous;
            public Scope( Action<Url> previous ) { _previous = previous; }
            public void Dispose() => _sSink = _previous;
        }

        /// <summary>
        /// Registers <paramref name="onUrlDiscovered"/> to be invoked for every exportable
        /// <see cref="Url"/> serialized on the current thread until the returned scope is disposed.
        /// </summary>
        public static IDisposable Begin( Action<Url> onUrlDiscovered )
        {
            var scope = new Scope( _sSink );
            _sSink = onUrlDiscovered;
            return scope;
        }

        internal static void Report( Url url )
        {
            if ( url.Export ) _sSink?.Invoke( url );
        }
    }

    public class UrlConverter : JsonConverter
    {
        private static string GetTimeHash()
        {
            return GetFileVersionHash( DateTime.UtcNow );
        }

        private static string GetFileVersionHash( DateTime timestamp )
        {
            var major = (int) (timestamp - new DateTime( 2000, 1, 1 )).TotalDays;
            var minor = (int) (timestamp - new DateTime( timestamp.Year, timestamp.Month, timestamp.Day )).TotalSeconds;
            return $"{major:x}-{minor:x}";
        }

        private static bool ShouldAppendVersionSuffix( Url url )
        {
            switch ( System.IO.Path.GetExtension( url ).ToLower() )
            {
                case ".png":
                    return false;
                default:
                    return true;
            }
        }

        public static string GetEncodedPath( Url url )
        {
            return string.Join( "/", url.Value.Split( '/' ).Select( WebUtility.UrlEncode ) );
        }

        public static string GetVersionSuffix( Url url )
        {
            return ShouldAppendVersionSuffix( url ) ? $"?v={GetTimeHash()}" : "";
        }

        public static string GetRootRelativeUrl( Url url )
        {
            return $"{GetEncodedPath( url )}{GetVersionSuffix( url )}";
        }

        /// <summary>
        /// Root-relative path for <paramref name="url"/>, reporting it to <see cref="UrlCrawl"/>.
        /// Unlike <see cref="WriteJson"/>, this returns a plain string rather than a
        /// <c>{"$url": ...}</c> wrapper, for embedding directly where there's no runtime JSON
        /// layer to resolve the wrapper.
        /// </summary>
        public static string RegisterAndGetRootRelativeUrl( Url url )
        {
            UrlCrawl.Report( url );
            return GetRootRelativeUrl( url );
        }

        public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
        {
            var url = (Url) value;

            if ( url.Value == null )
            {
                writer.WriteNull();
                return;
            }

            var rootRelative = RegisterAndGetRootRelativeUrl( url );

            writer.WriteStartObject();
            writer.WritePropertyName( "$url" );
            writer.WriteValue( rootRelative );
            writer.WriteEndObject();
        }

        public override object ReadJson( JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer )
        {
            return new Url( reader.ReadAsString() );
        }

        public override bool CanConvert( Type objectType )
        {
            return objectType == typeof(Url);
        }
    }
}
