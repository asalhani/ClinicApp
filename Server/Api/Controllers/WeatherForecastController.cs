using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using Entities.CustomExceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ClinicApp.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly IRepositoryManager _repository;

        public WeatherForecastController(IRepositoryManager repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
    
            var test = "test var value";
            var position = new { Latitude = 25, Longitude = 134 };
            //throw new KeyNotFoundException("not found exception");
            throw new AppException("app exception message {0} - {1}", test, position);
            Log.Logger.Warning("sending email... {@Position}", position);            
            //_repository.Company.AnyMethodFromCompanyRepository();
            //_repository.Employee.AnyMethodFromEmployeeRepository();

            return new string[] { "value1", "value2" };
        }
    }
}
