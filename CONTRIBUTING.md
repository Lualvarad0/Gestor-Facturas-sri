# Cómo contribuir a Nudo

¡Gracias por querer mejorar Nudo! Antes de enviar un cambio:

1. **Abre un issue** para conversar la idea o el error antes de un cambio grande.
2. **Crea una rama** desde `main` y mantén el estilo del código que la rodea (español en nombres y textos, Angular con signals, Tailwind sin CSS repetido).
3. **Agrega o actualiza pruebas** en `Back.Tests` cuando cambies el backend, y verifica que `dotnet test Back.Tests` y `npm run build` (en `Web/`) pasen.
4. **Abre el pull request** explicando qué cambia y por qué.

## Acuerdo de contribución (CLA)

Al abrir tu primer pull request, un bot te pedirá aceptar el acuerdo de contribución. Al aceptarlo:

- Confirmas que el código es tuyo (o tienes derecho a aportarlo).
- Mantienes los derechos de autor de tu aporte.
- Otorgas a Cornerstone Develop una licencia perpetua, mundial y no exclusiva para usar, modificar y distribuir tu aporte, también bajo otras licencias (por ejemplo, en la versión comercial de Nudo).

Sin el CLA aceptado no podemos integrar el cambio.

## Seguridad

Si encuentras una vulnerabilidad, no abras un issue público: escríbenos de forma privada desde la página de contacto de [Cornerstone Develop](https://portafolio-hwaq.vercel.app/).
