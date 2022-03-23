using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Entities.CustomExceptions;
using Entities.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using Serilog.Context;

namespace ClinicApp.Api.Middleware
{
    public class ErrorHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception error)
            {
                var errorResponse = new ErrorResponseDto()
                {
                    ErrorId = Guid.NewGuid().ToString(),
                };
                using (LogContext.PushProperty("ErrorID", errorResponse.ErrorId))
                using (LogContext.PushProperty("RequestMethod", context.Request.Method))
                using (LogContext.PushProperty("RequestPath", GetPath(context)))
                using (LogContext.PushProperty("RequestHeaders", FormatHeaders(context.Request.Headers)))
                using (LogContext.PushProperty("RequestBody",await ReadBodyFromRequest(context.Request)))
                using (LogContext.PushProperty("Host", context.Request.Host))
                {
                    Log.Logger.Error(error, $"{error.Message} - ErrorId: {errorResponse.ErrorId}");

                    var response = context.Response;
                    response.ContentType = "application/json";


                    switch (error)
                    {
                        case AppException e:
                            // custom application error
                            errorResponse.StatusCode = (int) HttpStatusCode.BadRequest;
                            errorResponse.ErrorMessage = $"{e.Message}. ErrorId: [{errorResponse.ErrorId}]";
                            errorResponse.ErrorDetails = e.ToString();
                            break;
                        default:
                            // unhandled error
                            errorResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                            errorResponse.ErrorMessage =
                                $"Unhandled exception occured. Try again or contact system administrator. ErrorId: {errorResponse.ErrorId}";
                            errorResponse.ErrorDetails = error.ToString();
                            break;
                    }

                    response.StatusCode = errorResponse.StatusCode;
                    using (LogContext.PushProperty("StatusCode", response.StatusCode))
                    {
                        var result = JsonSerializer.Serialize(errorResponse);
                        await response.WriteAsync(result);
                    }
                }
            }
        }

        private static string FormatHeaders(IHeaderDictionary headers) => string.Join(", ", headers.Select(kvp => $"{{{kvp.Key}: {string.Join(", ", kvp.Value)}}}"));

        static string GetPath(HttpContext httpContext, bool includeQueryInRequestPath = true)
        {
            /*
                In some cases, like when running integration tests with WebApplicationFactory<T>
                the Path returns an empty string instead of null, in that case we can't use
                ?? as fallback.
            */
            var requestPath = includeQueryInRequestPath
                ? httpContext.Features.Get<IHttpRequestFeature>()?.RawTarget
                : httpContext.Features.Get<IHttpRequestFeature>()?.Path;
            if (string.IsNullOrEmpty(requestPath))
            {
                requestPath = httpContext.Request.Path.ToString();
            }

            return requestPath;
        }
        
        private static async Task<string> ReadBodyFromRequest(HttpRequest request)
        {
            var isJsonRequest = request.ContentType != null &&
                                request.ContentType.Contains("application/json");
            if(!isJsonRequest)
                return string.Empty;
            
            // Ensure the request's body can be read multiple times (for the next middlewares in the pipeline).
            request.EnableBuffering();

            using var streamReader = new StreamReader(request.Body, leaveOpen: true);
            var requestBody = await streamReader.ReadToEndAsync();

            // Reset the request's body stream position for next middleware in the pipeline.
            request.Body.Position = 0;
            return requestBody;
        }
    }
}