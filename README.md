# Nudo — ordena tus gastos con el SRI

Versión gratuita y de código abierto de **Nudo**: sube el reporte `.txt` de SRI en Línea o los XML de tus facturas del mes, verifica cada comprobante con el servicio público del SRI y ordena tus gastos por categoría.

- **Importa** el reporte de *Comprobantes electrónicos recibidos* (.txt), los XML de tus facturas o claves de acceso pegadas.
- **Verifica** cada comprobante con el servicio web público de autorización del SRI. No pide tu clave de SRI en Línea.
- **Ordena** tus gastos del mes por categoría (vivienda, salud, educación, alimentación, vestimenta, turismo). Categorizas un proveedor una vez y se recuerda.
- **Marca** las facturas que no están a tu nombre y deja fuera las que tú emites con tu RUC.

> ¿Facturas con tu RUC? La versión **Nudo Profesional** suma ingresos y clientes, IVA del mes, buzón de facturas automático por correo, filtros avanzados y, próximamente, facturación electrónica y declaración de IVA.

## Tecnologías

- **Frontend** (`Web/`): Angular 22 (standalone, signals, zoneless) + Tailwind CSS 4 + Lucide.
- **Backend** (`Back/`): .NET 8 + PostgreSQL, Entity Framework Core, autenticación JWT.
- **Pruebas** (`Back.Tests/`): xUnit con pruebas de integración contra PostgreSQL y un SRI simulado.

## Requisitos

- [Node.js](https://nodejs.org/) 22 o superior
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) y `dotnet-ef` (`dotnet tool install -g dotnet-ef`)
- [Docker](https://www.docker.com/) para PostgreSQL local

## Puesta en marcha

1. **PostgreSQL** (desde la raíz):

   ```bash
   docker compose up -d
   ```

2. **Secretos del backend** (desde `Back/`; quedan fuera del repositorio):

   ```bash
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=nudo;Username=nudo;Password=nudo_dev"
   dotnet user-secrets set "Jwt:Key" "<una clave aleatoria de al menos 32 caracteres>"
   ```

3. **Base de datos y API** (desde `Back/`):

   ```bash
   dotnet ef database update
   dotnet run
   ```

   La API queda en `http://localhost:5000` (Swagger en `/swagger`).

4. **Frontend** (desde `Web/`):

   ```bash
   npm install
   npm start
   ```

   La app queda en `http://localhost:4200`.

### Pruebas

Con PostgreSQL levantado, desde la raíz:

```bash
dotnet test Back.Tests
```

Crean y borran su propia base `nudo_test`. Para usar otro servidor, define `NUDO_TEST_DB` (por ejemplo `Host=localhost;Port=5432;Username=nudo;Password=nudo_dev`).

## Cómo descargar el reporte del SRI

1. Ingresa a **SRI en Línea** con tu RUC o cédula.
2. Ve a **Facturación electrónica → Comprobantes electrónicos recibidos**.
3. Elige el año, el mes y el tipo **Todos**, y consulta.
4. Descarga el **reporte (.txt)** y súbelo en *Traer facturas*.

## API

| Método | Ruta | Uso |
|---|---|---|
| `POST` | `/api/auth/register`, `/api/auth/login` | Cuenta y sesión |
| `GET/PUT` | `/api/perfil` | Cédula, RUC y razón social |
| `POST` | `/api/comprobantes/importar` | Reporte .txt y/o XML (multipart) |
| `POST` | `/api/comprobantes/importar/claves` | Claves de acceso como texto |
| `GET` | `/api/comprobantes?anio=&mes=&categoria=&ruc=&q=` | Gastos del período |
| `GET` | `/api/comprobantes/resumen?anio=&mes=` | Totales por categoría y proveedor |
| `PUT` | `/api/comprobantes/{id}/categoria` | Categoría (se recuerda por proveedor) |
| `GET/PUT` | `/api/comprobantes/proveedores` | Proveedores y su categoría |

## Contribuir

Las contribuciones son bienvenidas. Lee [CONTRIBUTING.md](CONTRIBUTING.md) antes de abrir un pull request: te pediremos aceptar el acuerdo de contribución (CLA).

## Licencia y marca

El código se publica bajo la licencia **GNU AGPL-3.0** (ver `LICENSE`). Si ofreces una versión modificada como servicio en internet, debes publicar su código fuente con la misma licencia.

El nombre **Nudo**, su logo y su identidad visual no están incluidos en la licencia: no pueden usarse para identificar productos o servicios derivados sin autorización.

---

© 2026 Cornerstone Develop · Diseño y desarrollo: [Cornerstone Develop](https://portafolio-hwaq.vercel.app/)
