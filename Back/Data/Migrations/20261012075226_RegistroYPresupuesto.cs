using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Back.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistroYPresupuesto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "nombre",
                table: "usuarios",
                type: "character varying(121)",
                maxLength: 121,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "apellidos",
                table: "usuarios",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_nacimiento",
                table: "usuarios",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ingreso_mensual",
                table: "usuarios",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombres",
                table: "usuarios",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            // Cuentas existentes: el nombre que dieron al registrarse pasa a "nombres"; los apellidos se completan en Configuración.
            migrationBuilder.Sql("UPDATE usuarios SET nombres = left(nombre, 60);");

            migrationBuilder.CreateTable(
                name: "rubros",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    limite = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rubros", x => x.id);
                    table.ForeignKey(
                        name: "fk_rubros_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proveedores_rubro",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    rubro_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proveedores_rubro", x => x.id);
                    table.ForeignKey(
                        name: "fk_proveedores_rubro_rubros_rubro_id",
                        column: x => x.rubro_id,
                        principalTable: "rubros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_proveedores_rubro_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_proveedores_rubro_rubro_id",
                table: "proveedores_rubro",
                column: "rubro_id");

            migrationBuilder.CreateIndex(
                name: "ix_proveedores_rubro_usuario_id_ruc",
                table: "proveedores_rubro",
                columns: new[] { "usuario_id", "ruc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rubros_usuario_id_nombre",
                table: "rubros",
                columns: new[] { "usuario_id", "nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "proveedores_rubro");

            migrationBuilder.DropTable(
                name: "rubros");

            migrationBuilder.DropColumn(
                name: "apellidos",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "fecha_nacimiento",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "ingreso_mensual",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "nombres",
                table: "usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "nombre",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(121)",
                oldMaxLength: 121);
        }
    }
}
