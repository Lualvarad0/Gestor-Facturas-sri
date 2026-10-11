using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Back.Helpers
{
    /// <summary>
    /// Convierte errores esperados (reglas de negocio, concurrencia) en ProblemDetails
    /// sin registrarlos como fallos del servidor. Lo inesperado sigue al UseExceptionHandler.
    /// </summary>
    public class BusinessExceptionFilter : IExceptionFilter
    {
        private readonly ProblemDetailsFactory _problemDetailsFactory;

        public BusinessExceptionFilter(ProblemDetailsFactory problemDetailsFactory)
        {
            _problemDetailsFactory = problemDetailsFactory;
        }

        public void OnException(ExceptionContext context)
        {
            var (status, detalle) = context.Exception switch
            {
                BusinessException be => (be.StatusCode, be.Message),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                    "Los datos cambiaron mientras se procesaba la solicitud. Inténtalo de nuevo."),
                _ => (0, null),
            };
            if (detalle is null) return;

            var problema = _problemDetailsFactory.CreateProblemDetails(context.HttpContext, status, detail: detalle);
            context.Result = new ObjectResult(problema) { StatusCode = status };
            context.ExceptionHandled = true;
        }
    }
}
