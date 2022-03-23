namespace Entities.Appsettings
{
    /// <summary>
    /// In the target service, for example: InspectionProcessService make sure you provide
    /// settings inside the Serilog section.
    /// For example:
    ///     "Internal": {
    ///          "Enable": true,
    ///          "SerilogExceptionsFile": "logs/InternalSerilogExceptions.log",
    ///          "RotateSizeMB": 0.5,
    ///          "HeartBeatMilliSeconds": 30
    ///     },
    ///     
    /// Later on, if for some reason Serilog failed to log to one or all of its sinks, it will log that failure.
    /// </summary>
    public class InternalSerilogConfig
    {
        public bool Enable { get; set; } = false;
        public string SerilogExceptionsFile { get; set; }
        public float RotateSizeMB { get; set; } = 0.5f;
        public int HeartBeatMilliSeconds { get; set; } = 30; // The rate (seconds) at which the rotation logic is checked for.
        // To keep the server healthy, try to make it at least 1 minute.
    }

}