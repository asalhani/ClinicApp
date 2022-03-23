using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Entities.CustomExceptions;
using Entities.DTO;
using Microsoft.AspNetCore.Http;
using Serilog;

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
                
                Log.Logger.Error(error, $"{error.Message} - Error ID: {errorResponse.ErrorId}");
                
                var response = context.Response;
                response.ContentType = "application/json";
                

                switch(error)
                {
                    case AppException e:
                        // custom application error
                        errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        errorResponse.ErrorMessage = $"{e.Message}. Error ID: [{errorResponse.ErrorId}]" ;
                        errorResponse.ErrorDetails = e.ToString();
                        break;
                    default:
                        // unhandled error
                        errorResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                        errorResponse.ErrorMessage =
                            $"Unhandled exception occured. Try again or contact system administrator. Error ID: {errorResponse.ErrorId}";
                        errorResponse.ErrorDetails = error.ToString();
                        break;
                }

                response.StatusCode = errorResponse.StatusCode;
                var result = JsonSerializer.Serialize(errorResponse);
                await response.WriteAsync(result);
            }
        }
    }
}