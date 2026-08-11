using System;
using System.IO;
using System.Net;
using System.Text;

namespace SourceUtils.WebExport.Hosting
{
    /// <summary>
    /// An exception that terminates request handling with a specific HTTP status code.
    /// </summary>
    public sealed class ControllerActionException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public ControllerActionException( HttpStatusCode statusCode, string message )
            : base( message )
        {
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// Base class for types containing action methods that handle HTTP GET requests,
    /// grouped by a common URL prefix via <see cref="PrefixAttribute"/>. A new instance
    /// is constructed for each request.
    /// </summary>
    public abstract class Controller
    {
        internal HttpListenerContext HttpContext { get; set; }

        public HttpListenerRequest Request => HttpContext.Request;
        public HttpListenerResponse Response => HttpContext.Response;

        protected static ControllerActionException NotFoundException()
        {
            return new ControllerActionException( HttpStatusCode.NotFound, "File not found." );
        }

        /// <summary>
        /// Writes the return value of an invoked action method to <see cref="Response"/>.
        /// </summary>
        protected internal virtual void WriteResult( object value, Type declaredReturnType )
        {
            if ( declaredReturnType == typeof(void) ) return;

            if ( declaredReturnType == typeof(string) )
            {
                OnServiceText( (string) value );
                return;
            }

            throw new NotSupportedException( $"No response writer available for return type '{declaredReturnType}'." );
        }

        /// <summary>
        /// Default response writer for action methods that return a <see cref="string"/>.
        /// </summary>
        protected virtual void OnServiceText( string text )
        {
            try
            {
                Response.ContentType = MimeTypes.MimeTypeMap.GetMimeType( Path.GetExtension( Request.Url.AbsolutePath ) );

                var bytes = Encoding.UTF8.GetBytes( text ?? "" );
                Response.OutputStream.Write( bytes, 0, bytes.Length );
            }
            catch ( HttpListenerException )
            {
                //
            }
        }
    }
}
