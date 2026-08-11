using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SourceUtils.WebExport.Hosting
{
    /// <summary>
    /// Routes incoming HTTP requests to matching action methods in <see cref="Controller"/>
    /// subclasses, based on <see cref="PrefixAttribute"/> and <see cref="GetAttribute"/>.
    /// </summary>
    public sealed class ControllerMap
    {
        private sealed class Route
        {
            public Type ControllerType;
            public MethodInfo Method;
            public ParameterInfo[] Parameters;
            public Regex Regex;
            public string RequiredExtension;
            public int SegmentCount;
        }

        // Full-path routes (GetAttribute.MatchAllUrl == true, the default) are tried first,
        // since they're always more specific than a prefix route.
        private readonly List<Route> _exactRoutes = new List<Route>();

        // Prefix routes (MatchAllUrl == false) are tried in order of decreasing specificity
        // (segment count), so e.g. "/materials/{...}" is tried before a catch-all "/".
        private readonly List<Route> _prefixRoutes = new List<Route>();

        public void Add<TController>( string prefix ) where TController : Controller, new()
        {
            RegisterController( typeof(TController), prefix, null );
        }

        public void Add( Assembly assembly )
        {
            foreach ( var type in assembly.GetTypes() )
            {
                if ( !typeof(Controller).IsAssignableFrom( type ) ) continue;

                var prefixes = type.GetCustomAttributes<PrefixAttribute>().ToArray();
                if ( prefixes.Length == 0 ) continue;

                foreach ( var prefix in prefixes )
                {
                    RegisterController( type, prefix.Value, prefix.Extension );
                }
            }
        }

        private void RegisterController( Type controllerType, string prefixValue, string prefixExtension )
        {
            foreach ( var method in controllerType.GetMethods( BindingFlags.Public | BindingFlags.Instance ) )
            {
                var get = method.GetCustomAttribute<GetAttribute>();
                if ( get == null ) continue;

                var pattern = prefixValue.TrimEnd( '/' ) + (get.Pattern ?? "");
                var extension = get.Extension ?? prefixExtension;
                var exact = get.MatchAllUrl;

                var parameters = method.GetParameters();
                foreach ( var param in parameters )
                {
                    if ( param.GetCustomAttribute<UrlAttribute>() == null )
                    {
                        throw new NotSupportedException(
                            $"Parameter '{param.Name}' on {controllerType.Name}.{method.Name} must be annotated with [Url]." );
                    }
                }

                var route = new Route
                {
                    ControllerType = controllerType,
                    Method = method,
                    Parameters = parameters,
                    Regex = BuildRegex( pattern, exact ),
                    RequiredExtension = extension,
                    SegmentCount = CountSegments( pattern )
                };

                if ( exact )
                {
                    _exactRoutes.Add( route );
                }
                else
                {
                    _prefixRoutes.Add( route );
                    _prefixRoutes.Sort( ( a, b ) => b.SegmentCount - a.SegmentCount );
                }
            }
        }

        private static string[] GetSegments( string pattern )
        {
            return pattern.Trim( '/' ).Split( '/', StringSplitOptions.RemoveEmptyEntries );
        }

        private static int CountSegments( string pattern )
        {
            return GetSegments( pattern ).Length;
        }

        private static Regex BuildRegex( string pattern, bool exact )
        {
            var sb = new StringBuilder( "^" );

            foreach ( var segment in GetSegments( pattern ) )
            {
                sb.Append( '/' );
                AppendSegmentPattern( sb, segment );
            }

            sb.Append( exact ? "$" : "(?:/.*)?$" );

            return new Regex( sb.ToString(), RegexOptions.Compiled );
        }

        private static void AppendSegmentPattern( StringBuilder sb, string segment )
        {
            var i = 0;
            while ( i < segment.Length )
            {
                var open = segment.IndexOf( '{', i );
                if ( open < 0 )
                {
                    sb.Append( Regex.Escape( segment.Substring( i ) ) );
                    break;
                }

                if ( open > i ) sb.Append( Regex.Escape( segment.Substring( i, open - i ) ) );

                var close = segment.IndexOf( '}', open );
                var name = segment.Substring( open + 1, close - open - 1 );

                sb.Append( "(?<" ).Append( name ).Append( ">[^/]+?)" );

                i = close + 1;
            }
        }

        private static object BindParameter( ParameterInfo parameter, Match match )
        {
            var group = match.Groups[parameter.Name];
            var raw = group.Success ? group.Value : null;

            if ( parameter.ParameterType == typeof(string) ) return raw;

            if ( parameter.ParameterType == typeof(int) )
            {
                if ( raw == null )
                {
                    throw new InvalidOperationException( $"Missing required route value '{parameter.Name}'." );
                }

                return int.Parse( raw, CultureInfo.InvariantCulture );
            }

            throw new NotSupportedException(
                $"Unsupported route parameter type '{parameter.ParameterType}' for '{parameter.Name}'." );
        }

        private static void WriteBody( HttpListenerResponse response, string text )
        {
            try
            {
                response.ContentType = "text/plain";
                var bytes = Encoding.UTF8.GetBytes( text ?? "" );
                response.OutputStream.Write( bytes, 0, bytes.Length );
            }
            catch ( HttpListenerException )
            {
                //
            }
        }

        private static void SafeClose( HttpListenerResponse response )
        {
            try
            {
                response.OutputStream.Close();
            }
            catch ( Exception )
            {
                // Client may have already disconnected.
            }
        }

        private void InvokeRoute( Route route, HttpListenerContext context, Match match )
        {
            var controller = (Controller) Activator.CreateInstance( route.ControllerType );
            controller.HttpContext = context;

            var args = new object[route.Parameters.Length];
            for ( var i = 0; i < route.Parameters.Length; ++i )
            {
                args[i] = BindParameter( route.Parameters[i], match );
            }

            object result;
            try
            {
                result = route.Method.Invoke( controller, args );
            }
            catch ( TargetInvocationException e ) when ( e.InnerException != null )
            {
                throw e.InnerException;
            }

            controller.WriteResult( result, route.Method.ReturnType );
        }

        internal void Dispatch( HttpListenerContext context )
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if ( request.HttpMethod != "GET" )
                {
                    response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
                    WriteBody( response, "Method not allowed." );
                    return;
                }

                var path = request.Url.AbsolutePath;

                foreach ( var route in _exactRoutes )
                {
                    var match = route.Regex.Match( path );
                    if ( !match.Success ) continue;

                    InvokeRoute( route, context, match );
                    return;
                }

                foreach ( var route in _prefixRoutes )
                {
                    if ( route.RequiredExtension != null &&
                         !path.EndsWith( route.RequiredExtension, StringComparison.OrdinalIgnoreCase ) )
                    {
                        continue;
                    }

                    var match = route.Regex.Match( path );
                    if ( !match.Success ) continue;

                    InvokeRoute( route, context, match );
                    return;
                }

                response.StatusCode = (int) HttpStatusCode.NotFound;
                WriteBody( response, "File not found." );
            }
            catch ( ControllerActionException e )
            {
                response.StatusCode = (int) e.StatusCode;
                WriteBody( response, e.Message );
            }
            catch ( HttpListenerException )
            {
                // Client disconnected mid-response.
            }
            catch ( Exception e )
            {
                try
                {
                    response.StatusCode = (int) HttpStatusCode.InternalServerError;
                    WriteBody( response, e.ToString() );
                }
                catch ( Exception )
                {
                    //
                }
            }
            finally
            {
                SafeClose( response );
            }
        }
    }

    /// <summary>
    /// Wraps a <see cref="HttpListener"/>, dispatching each accepted request to a
    /// thread-pool worker so multiple requests can be served concurrently.
    /// </summary>
    public sealed class Server
    {
        private readonly HttpListener _listener = new HttpListener();

        public HttpListenerPrefixCollection Prefixes => _listener.Prefixes;

        public ControllerMap Controllers { get; } = new ControllerMap();

        public Server()
        {
        }

        public Server( int port )
        {
            Prefixes.Add( $"http://localhost:{port}/" );
        }

        public void Run()
        {
            _listener.Start();

            while ( true )
            {
                HttpListenerContext context;

                try
                {
                    context = _listener.GetContext();
                }
                catch ( HttpListenerException )
                {
                    break;
                }
                catch ( ObjectDisposedException )
                {
                    break;
                }

                var capturedContext = context;
                ThreadPool.QueueUserWorkItem( _ => Controllers.Dispatch( capturedContext ) );
            }
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}
