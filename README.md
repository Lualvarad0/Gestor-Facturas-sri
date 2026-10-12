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

## Cuentas y recuperación de contraseña

El acceso incluye login, registro y recuperación (`/recuperar` y `/restablecer`). Las nuevas contraseñas usan PBKDF2-SHA256 con sal aleatoria y 600.000 iteraciones. Se aceptan frases de 15 a 128 caracteres; el login mantiene compatibilidad con las cuentas BCrypt existentes y actualiza su hash tras un inicio de sesión correcto. Las cuentas públicas siempre reciben el rol `user`.

La API limita a 10 solicitudes de autenticación por minuto y dirección IP. Después de cinco claves incorrectas, bloquea el acceso a esa cuenta durante 15 minutos. La recuperación responde igual para correos registrados y desconocidos y admite un envío por cuenta cada dos minutos. Cada enlace contiene 256 bits aleatorios, vence a los 15 minutos, se almacena únicamente como hash y se consume mediante una actualización atómica. Cambiar la contraseña revoca los JWT anteriores. Las sesiones antiguas del navegador deberán iniciar sesión otra vez después de esta actualización.

Para activar el envío, configura `RecoveryMail:Host`, `Port` (587 con STARTTLS), `Username`, `Password`, `From` y `FrontendUrl`. Usa secretos del servidor o variables `RecoveryMail__...`, nunca credenciales en archivos versionados. `FrontendUrl` debe ser la URL HTTPS real del frontend; HTTP localhost solo se admite en desarrollo. Si falta la configuración, la API devuelve 503 y la pantalla informa que la recuperación no está disponible. El SMTP debe permitir STARTTLS; esta implementación no usa el puerto 465 con TLS implícito.

Aplica la migración `SeguridadAcceso` antes de servir solicitudes (`dotnet ef database update --project Back`). No cambia ni elimina contraseñas o facturas existentes. La nueva validación de sesiones consulta el sello de contraseña del usuario en cada solicitud protegida.

En producción, sirve frontend y API con HTTPS, restringe `Cors:Origins` y `AllowedHosts`, y configura el proxy de confianza para HTTPS y direcciones IP antes de publicar. Los límites IP son por instancia; varios servidores requieren un límite compartido en el proxy. La sesión del frontend se conserva en `sessionStorage`, que sigue siendo accesible al JavaScript del mismo origen: la protección frente a XSS y una política CSP del servidor web siguen siendo necesarias. El servicio de correo usa una cola acotada en memoria; un reinicio puede perder correos pendientes, por lo que un despliegue con requisitos de entrega garantizada debe usar una cola persistente. No incluye MFA ni verificación de correo.

Pruebas de seguridad sin PostgreSQL:

```bash
dotnet test Back.Tests --filter "FullyQualifiedName~AuthSecurityTests|FullyQualifiedName~AuthEndpointTests"
```

Las pruebas de integración `Recuperacion_*` y `Login_Bloquea*` necesitan el PostgreSQL de pruebas indicado arriba. Comprueban consumo concurrente del enlace, caducidad, respuestas genéricas, bloqueo y revocación de sesiones; el envío SMTP real requiere configurar el proveedor.

## Despliegue

Arquitectura gratuita de referencia: **Neon** (PostgreSQL), **Render** (API en Docker) y **Vercel** (frontend). El frontend llama directo a la API de Render con CORS; no se usan rewrites de Vercel hacia `/api`, porque todos los visitantes compartirían la IP del proxy y el límite de autenticación.

### Base de datos (Neon)

Crea el proyecto en la región AWS US East 1 y usa la conexión **directa** (sin *pooling*) en formato Npgsql:

```text
Host=<host>.us-east-1.aws.neon.tech;Database=<base>;Username=<usuario>;Password=<clave>;SSL Mode=Require
```

### API (Render)

`render.yaml` define un servicio web Docker (plan free, región virginia, health check en `/health`). El `Dockerfile` de la raíz compila también el frontend y lo sirve desde `wwwroot`, así que la imagen funciona sola. Variables:

| Variable | Valor |
|---|---|
| `ConnectionStrings__Default` | Cadena de Neon (se carga a mano en el panel) |
| `Jwt__Key` | Generada por Render |
| `Database__MigrateOnStartup` | `true` |
| `ForwardedHeaders__Enabled` | `true`: toma la IP y el esquema del proxy y no redirige a HTTPS |
| `ForwardedHeaders__ForwardLimit` | Opcional, número de proxies delante (por defecto `1`) |
| `PORT` | `8080` |
| `Cors__Origins__0` | Dominio de Vercel, con `https://` y sin barra final |
| `AllowedHosts` | Dominio de Render (por ejemplo `nudo-ab12.onrender.com`) |
| `RecoveryMail__...` | Opcional, ver *Cuentas y recuperación de contraseña*; `FrontendUrl` es el dominio de Vercel |

Prueba la imagen en local con `docker build -t nudo .`.

### Frontend (Vercel)

1. Pon la URL de Render en `apiUrl` de `Web/src/environments/environment.ts` (`https://<servicio>.onrender.com/api`).
2. Importa el repositorio con **Root Directory** `Web`, **Build Command** `npx ng build`, **Output Directory** `dist/web/browser` y **Node.js** 24.x (Angular exige 22.22.3 o superior).

`Web/vercel.json` redirige todas las rutas a `index.html` para que funcionen las rutas del SPA.

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
