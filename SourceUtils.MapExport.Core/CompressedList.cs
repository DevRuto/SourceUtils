using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SourceUtils.MapExport
{
    internal interface ICompressedList
    {
        int Count { get; }
        int MaxUncompressedCount { get; set; }
        void WriteRaw( JsonWriter writer, JsonSerializer serializer );
    }

    [JsonConverter( typeof(CompressedListConverter) )]
    public class CompressedList<T> : List<T>, ICompressedList
    {
        private class EnumerableHack : IEnumerable<T>
        {
            private readonly CompressedList<T> _list;

            public EnumerableHack( CompressedList<T> list )
            {
                _list = list;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private readonly EnumerableHack _enumerable;

        public CompressedList()
        {
            _enumerable = new EnumerableHack( this );
        }

        public CompressedList( IEnumerable<T> collection )
            : base( collection )
        {
            _enumerable = new EnumerableHack( this );
        }

        public int MaxUncompressedCount { get; set; } = 256;

        public virtual void WriteRaw( JsonWriter writer, JsonSerializer serializer )
        {
            serializer.Serialize( writer, _enumerable, typeof(IEnumerable<T>) );
        }
    }

    public class CompressedFloatList : CompressedList<float>
    {
        [ThreadStatic]
        private static StringBuilder _sStringBuilder;

        public string FormatString { get; set; } = "G";

        public override void WriteRaw( JsonWriter writer, JsonSerializer serializer )
        {
            if ( _sStringBuilder == null ) _sStringBuilder = new StringBuilder();
            else _sStringBuilder.Remove( 0, _sStringBuilder.Length );

            writer.WriteStartArray();

            if ( Count == 0 )
            {
                writer.WriteEndArray();
                return;
            }

            var formatString = FormatString;

            foreach ( var item in this )
            {
                var str = item.ToString( formatString, CultureInfo.InvariantCulture );
                _sStringBuilder.Append( str );
                _sStringBuilder.Append( "," );
            }

            writer.WriteRaw( _sStringBuilder.ToString( 0, _sStringBuilder.Length - 1 ) );
            writer.WriteEndArray();
        }
    }

    public class CompressedListConverter : JsonConverter
    {
        private struct TempWriter : IDisposable
        {
            private readonly StringWriter _textWriter;

            public readonly JsonTextWriter Writer;

            public TempWriter( StringWriter textWriter )
            {
                _textWriter = textWriter;
                Writer = new JsonTextWriter( textWriter );
            }

            public void Clear()
            {
                Writer.Flush();
                _textWriter.GetStringBuilder().Clear();
            }

            public string GetString()
            {
                Writer.Flush();
                return _textWriter.ToString();
            }

            public void Dispose()
            {
                ReleaseTempWriter( this );
            }
        }

        private const int MaxPoolSize = 256;

        [ThreadStatic]
        private static List<TempWriter> _sTempWriters;

        private static TempWriter GetTempWriter()
        {
            if ( _sTempWriters == null || _sTempWriters.Count == 0 )
            {
                return new TempWriter( new StringWriter() );
            }

            var last = _sTempWriters[_sTempWriters.Count - 1];
            _sTempWriters.RemoveAt( _sTempWriters.Count - 1 );

            last.Clear();

            return last;
        }

        private static void ReleaseTempWriter( TempWriter writer )
        {
            if ( _sTempWriters == null ) _sTempWriters = new List<TempWriter>();
            if ( _sTempWriters.Count >= MaxPoolSize ) return;

            _sTempWriters.Add( writer );
        }

        public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
        {
            var list = (ICompressedList) value;

            if ( list == null )
            {
                writer.WriteNull();
                return;
            }

            if ( list.Count <= list.MaxUncompressedCount )
            {
                list.WriteRaw( writer, serializer );
                return;
            }

            string raw;
            using ( var tempWriter = GetTempWriter() )
            {
                list.WriteRaw( tempWriter.Writer, serializer );
                raw = tempWriter.GetString();
            }

            writer.WriteValue( LZString.compressToBase64( raw ) );
        }

        public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert( Type objectType )
        {
            return typeof(ICompressedList).IsAssignableFrom( objectType );
        }
    }

    public class NiceArrayConverter : JsonConverter
    {
        public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
        {
            if ( value == null )
            {
                writer.WriteNull();
                return;
            }

            var floatArr = value as IEnumerable<float>;
            var intArr = value as IEnumerable<int>;
            var uintArr = value as IEnumerable<uint>;

            string raw;

            if ( floatArr != null )
            {
                raw = string.Join( ",", System.Linq.Enumerable.Select( floatArr, f => f.ToString( CultureInfo.InvariantCulture ) ) );
            }
            else if ( intArr != null )
            {
                raw = string.Join( ",", intArr );
            }
            else if ( uintArr != null )
            {
                raw = string.Join( ",", uintArr );
            }
            else
            {
                throw new NotImplementedException();
            }

            writer.WriteStartArray();
            writer.WriteRaw( raw );
            writer.WriteEndArray();
        }

        public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert( Type objectType )
        {
            return typeof(IEnumerable<float>).IsAssignableFrom( objectType ) ||
                   typeof(IEnumerable<int>).IsAssignableFrom( objectType ) ||
                   typeof(IEnumerable<uint>).IsAssignableFrom( objectType );
        }
    }

    /// <summary>
    /// Builds the shared JSON serializer used for every map-data payload, matching
    /// SourceUtils.WebExport.ResourceController's serializer configuration.
    /// </summary>
    public static class MapJsonSerializer
    {
        private static readonly JsonSerializer _sSerializer = Create();

        public static JsonSerializer Shared => _sSerializer;

        public static JsonSerializer Create()
        {
            var serializer = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            serializer.Converters.Add( new NiceArrayConverter() );

            return serializer;
        }

        public static JToken ToJToken( object value )
        {
            return value == null ? null : JObject.FromObject( value, Shared );
        }
    }
}
