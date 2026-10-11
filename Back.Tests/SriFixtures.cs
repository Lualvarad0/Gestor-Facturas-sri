using System.Security;
using Back.Sri;

namespace Back.Tests
{
    /// <summary>Genera claves de acceso válidas y XML/respuestas SOAP con el formato real del SRI.</summary>
    public static class SriFixtures
    {
        public const string RucFarmacia = "1790010937001";
        public const string RucSupermercado = "0990017514001";
        public const string MiCedula = "0912345675";
        public const string MiRuc = MiCedula + "001";

        public static string Clave(DateOnly fecha, string tipo, string ruc, int secuencial, string ambiente = "2")
        {
            var base48 = $"{fecha:ddMMyyyy}{tipo}{ruc}{ambiente}001001{secuencial:D9}12345678" + "1";
            return base48 + ClaveAcceso.DigitoVerificador(base48);
        }

        public static string FacturaXml(string clave, string ruc, string razonSocial, DateOnly fecha,
            string comprador = MiCedula, string razonComprador = "DAVID ALVARADO") => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <factura id="comprobante" version="2.1.0">
              <infoTributaria>
                <ambiente>2</ambiente><tipoEmision>1</tipoEmision>
                <razonSocial>{razonSocial}</razonSocial><nombreComercial>{razonSocial} Comercial</nombreComercial>
                <ruc>{ruc}</ruc><claveAcceso>{clave}</claveAcceso><codDoc>01</codDoc>
                <estab>001</estab><ptoEmi>001</ptoEmi><secuencial>{clave.Substring(30, 9)}</secuencial>
                <dirMatriz>Guayaquil</dirMatriz>
              </infoTributaria>
              <infoFactura>
                <fechaEmision>{fecha:dd/MM/yyyy}</fechaEmision>
                <tipoIdentificacionComprador>05</tipoIdentificacionComprador>
                <razonSocialComprador>{razonComprador}</razonSocialComprador>
                <identificacionComprador>{comprador}</identificacionComprador>
                <totalSinImpuestos>30.00</totalSinImpuestos>
                <totalDescuento>1.50</totalDescuento>
                <totalConImpuestos>
                  <totalImpuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><baseImponible>20.00</baseImponible><tarifa>15</tarifa><valor>3.00</valor></totalImpuesto>
                  <totalImpuesto><codigo>2</codigo><codigoPorcentaje>0</codigoPorcentaje><baseImponible>10.00</baseImponible><tarifa>0</tarifa><valor>0.00</valor></totalImpuesto>
                </totalConImpuestos>
                <propina>0.00</propina>
                <importeTotal>33.00</importeTotal>
                <moneda>DOLAR</moneda>
              </infoFactura>
              <detalles>
                <detalle><codigoPrincipal>A1</codigoPrincipal><descripcion>Paracetamol 500mg</descripcion><cantidad>2.000000</cantidad><precioUnitario>5.000000</precioUnitario><descuento>0.00</descuento><precioTotalSinImpuesto>10.00</precioTotalSinImpuesto></detalle>
                <detalle><codigoPrincipal>B2</codigoPrincipal><descripcion>Vitamina C &amp; Zinc</descripcion><cantidad>1.000000</cantidad><precioUnitario>21.500000</precioUnitario><descuento>1.50</descuento><precioTotalSinImpuesto>20.00</precioTotalSinImpuesto></detalle>
              </detalles>
            </factura>
            """;

        public static string NotaCreditoXml(string clave, string ruc, DateOnly fecha) => $"""
            <notaCredito id="comprobante" version="1.1.0">
              <infoTributaria>
                <ambiente>2</ambiente><tipoEmision>1</tipoEmision><razonSocial>FARMACIA DE PRUEBA S.A.</razonSocial>
                <ruc>{ruc}</ruc><claveAcceso>{clave}</claveAcceso><codDoc>04</codDoc>
                <estab>001</estab><ptoEmi>001</ptoEmi><secuencial>{clave.Substring(30, 9)}</secuencial>
              </infoTributaria>
              <infoNotaCredito>
                <fechaEmision>{fecha:dd/MM/yyyy}</fechaEmision>
                <razonSocialComprador>DAVID ALVARADO</razonSocialComprador>
                <identificacionComprador>{MiCedula}</identificacionComprador>
                <codDocModificado>01</codDocModificado><numDocModificado>001-001-000000001</numDocModificado>
                <totalSinImpuestos>10.00</totalSinImpuestos>
                <valorModificacion>11.50</valorModificacion>
                <totalConImpuestos>
                  <totalImpuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><baseImponible>10.00</baseImponible><valor>1.50</valor></totalImpuesto>
                </totalConImpuestos>
                <motivo>Devolución</motivo>
              </infoNotaCredito>
              <detalles>
                <detalle><codigoInterno>B2</codigoInterno><descripcion>Vitamina C</descripcion><cantidad>1</cantidad><precioUnitario>10.00</precioUnitario><descuento>0</descuento><precioTotalSinImpuesto>10.00</precioTotalSinImpuesto></detalle>
              </detalles>
            </notaCredito>
            """;

        public static string RespuestaAutorizada(string clave, string comprobanteXml) => $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><ns2:autorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.autorizacion"><RespuestaAutorizacionComprobante><claveAccesoConsultada>{clave}</claveAccesoConsultada><numeroComprobantes>1</numeroComprobantes><autorizaciones><autorizacion><estado>AUTORIZADO</estado><numeroAutorizacion>{clave}</numeroAutorizacion><fechaAutorizacion>2026-10-05T10:22:33-05:00</fechaAutorizacion><ambiente>PRODUCCIÓN</ambiente><comprobante><![CDATA[{comprobanteXml}]]></comprobante><mensajes/></autorizacion></autorizaciones></RespuestaAutorizacionComprobante></ns2:autorizacionComprobanteResponse></soap:Body></soap:Envelope>
            """;

        public static string RespuestaNoAutorizada(string clave) => $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><ns2:autorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.autorizacion"><RespuestaAutorizacionComprobante><claveAccesoConsultada>{clave}</claveAccesoConsultada><numeroComprobantes>1</numeroComprobantes><autorizaciones><autorizacion><estado>NO AUTORIZADO</estado><fechaAutorizacion>2026-10-05T10:22:33-05:00</fechaAutorizacion><ambiente>PRODUCCIÓN</ambiente><comprobante>{SecurityElement.Escape("<factura/>")}</comprobante><mensajes><mensaje><identificador>39</identificador><mensaje>FIRMA INVALIDA</mensaje><informacionAdicional>firma no válida</informacionAdicional><tipo>ERROR</tipo></mensaje></mensajes></autorizacion></autorizaciones></RespuestaAutorizacionComprobante></ns2:autorizacionComprobanteResponse></soap:Body></soap:Envelope>
            """;

        // Respuesta real capturada del SRI para una clave inexistente.
        public static string RespuestaNoEncontrada(string clave) =>
            $"<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><ns2:autorizacionComprobanteResponse xmlns:ns2=\"http://ec.gob.sri.ws.autorizacion\"><RespuestaAutorizacionComprobante><claveAccesoConsultada>{clave}</claveAccesoConsultada><numeroComprobantes>0</numeroComprobantes><autorizaciones/></RespuestaAutorizacionComprobante></ns2:autorizacionComprobanteResponse></soap:Body></soap:Envelope>";
    }
}
