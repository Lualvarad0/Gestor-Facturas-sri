using Back.Dtos;
using Back.Helpers;
using Back.Interfaces;
using Back.Models;
using Back.Sri;

namespace Back.Services
{
    public class PerfilService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IComprobanteRepository _comprobanteRepository;

        public PerfilService(IUsuarioRepository usuarioRepository, IComprobanteRepository comprobanteRepository)
        {
            _usuarioRepository = usuarioRepository;
            _comprobanteRepository = comprobanteRepository;
        }

        public async Task<PerfilDto> ObtenerAsync(int usuarioId) => PerfilDto.From(await ObtenerUsuarioAsync(usuarioId));

        public async Task<PerfilActualizadoDto> ActualizarAsync(int usuarioId, PerfilRequest request)
        {
            var cedula = Normalizar(request.Cedula);
            var ruc = Normalizar(request.Ruc);

            if (cedula is not null && !Identificacion.EsCedulaValida(cedula))
                throw new BusinessException("La cédula no es válida. Revisa los 10 dígitos.");
            if (ruc is not null && !Identificacion.EsRucValido(ruc))
                throw new BusinessException("El RUC no es válido. Debe tener 13 dígitos y terminar en el número de establecimiento (por ejemplo, 001).");

            if (ruc is not null && Identificacion.EsPersonaNatural(ruc))
            {
                // El RUC de persona natural es la cédula + establecimiento: se completan entre sí.
                if (cedula is null) cedula = ruc[..10];
                else if (ruc[..10] != cedula)
                    throw new BusinessException("El RUC de persona natural debe empezar con tu número de cédula.");
            }

            var usuario = await ObtenerUsuarioAsync(usuarioId);
            var rucAnterior = usuario.Ruc;
            var razonSocial = string.IsNullOrWhiteSpace(request.RazonSocial) ? null : request.RazonSocial.Trim().ToUpperInvariant();


            usuario.Cedula = cedula;
            usuario.Ruc = ruc;
            usuario.RazonSocial = razonSocial;
            usuario.Regimen = request.Regimen;
            usuario.CargasFamiliares = request.CargasFamiliares;
            usuario.EnfermedadCatastrofica = request.EnfermedadCatastrofica;
            await _usuarioRepository.UpdateAsync(usuario);

            // Con un RUC nuevo, lo que el usuario emitió pasa a ser ingreso (y viceversa).
            var (emitidos, recibidos) = rucAnterior == ruc
                ? (-1, -1)
                : await _comprobanteRepository.ReclasificarAsync(usuarioId, ruc);

            return new PerfilActualizadoDto(PerfilDto.From(usuario), emitidos, recibidos);
        }

        private async Task<Usuario> ObtenerUsuarioAsync(int usuarioId) =>
            await _usuarioRepository.GetByIdAsync(usuarioId) ?? throw BusinessException.NotFound("Usuario no encontrado.");

        private static string? Normalizar(string? valor)
        {
            var limpio = new string((valor ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            return limpio.Length == 0 ? null : limpio;
        }
    }
}
