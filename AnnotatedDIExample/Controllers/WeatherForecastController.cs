using AnnotatedDIExample.Services;
using Microsoft.AspNetCore.Mvc;

namespace AnnotatedDIExample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IDevOnlyService devonly;
        private readonly IMyService myService;

        public WeatherForecastController(ILogger<WeatherForecastController> logger,
            IDevOnlyService devonly, IMyService myService)
        {
            _logger = logger;
            this.devonly = devonly;
            this.myService = myService;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            myService.Execute();
            devonly.Execute();

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
