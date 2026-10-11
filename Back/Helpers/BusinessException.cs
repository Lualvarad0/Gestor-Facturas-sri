namespace Back.Helpers
{
    /// <summary>Error de regla de negocio que se devuelve al cliente como ProblemDetails.</summary>
    public class BusinessException : Exception
    {
        public int StatusCode { get; }

        public BusinessException(string message, int statusCode = StatusCodes.Status400BadRequest) : base(message)
        {
            StatusCode = statusCode;
        }

        public static BusinessException NotFound(string message) => new(message, StatusCodes.Status404NotFound);
        public static BusinessException Conflict(string message) => new(message, StatusCodes.Status409Conflict);
    }
}
