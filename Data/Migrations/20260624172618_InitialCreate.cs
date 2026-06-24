using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RouteGen.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pontos_embarque",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlunoId = table.Column<long>(type: "bigint", nullable: false),
                    Matricula = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Endereco = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pontos_embarque", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VeiculoId = table.Column<long>(type: "bigint", nullable: false),
                    RotaTransporte = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CursoId = table.Column<long>(type: "bigint", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    DistanciaTotalMetros = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DestinoLatitude = table.Column<double>(type: "double precision", nullable: false),
                    DestinoLongitude = table.Column<double>(type: "double precision", nullable: false),
                    DestinoNome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "paradas_rota",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RotaId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<long>(type: "bigint", nullable: false),
                    Matricula = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ClusterId = table.Column<int>(type: "integer", nullable: false),
                    Confirmada = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paradas_rota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paradas_rota_rotas_RotaId",
                        column: x => x.RotaId,
                        principalTable: "rotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paradas_rota_RotaId_AlunoId",
                table: "paradas_rota",
                columns: new[] { "RotaId", "AlunoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pontos_embarque_AlunoId",
                table: "pontos_embarque",
                column: "AlunoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rotas_VeiculoId_Data",
                table: "rotas",
                columns: new[] { "VeiculoId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paradas_rota");

            migrationBuilder.DropTable(
                name: "pontos_embarque");

            migrationBuilder.DropTable(
                name: "rotas");
        }
    }
}
