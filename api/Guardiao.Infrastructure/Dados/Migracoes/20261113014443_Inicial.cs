using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardiao.Infrastructure.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Motoboys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Placa = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    DispositivoId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    DispositivoAtivo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CadastradoEm = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Telefone = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Motoboys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZonasDeRisco",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZonaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    FaixaHoraria = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Nivel = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZonasDeRisco", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContatosEmergencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Parentesco = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    MotoboyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Telefone = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContatosEmergencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContatosEmergencia_Motoboys_MotoboyId",
                        column: x => x.MotoboyId,
                        principalTable: "Motoboys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ocorrencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MotoboyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    IntensidadeG = table.Column<double>(type: "REAL", nullable: true),
                    DataHora = table.Column<string>(type: "TEXT", nullable: false),
                    RecebidaEm = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    NotificadaEm = table.Column<string>(type: "TEXT", nullable: true),
                    ContatosNotificados = table.Column<int>(type: "INTEGER", nullable: false),
                    EncerradaEm = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ocorrencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ocorrencias_Motoboys_MotoboyId",
                        column: x => x.MotoboyId,
                        principalTable: "Motoboys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContatosEmergencia_MotoboyId",
                table: "ContatosEmergencia",
                column: "MotoboyId");

            migrationBuilder.CreateIndex(
                name: "IX_Motoboys_DispositivoId",
                table: "Motoboys",
                column: "DispositivoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Motoboys_Placa",
                table: "Motoboys",
                column: "Placa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_MotoboyId_DataHora",
                table: "Ocorrencias",
                columns: new[] { "MotoboyId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_ZonasDeRisco_FaixaHoraria_Latitude_Longitude",
                table: "ZonasDeRisco",
                columns: new[] { "FaixaHoraria", "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_ZonasDeRisco_ZonaId_FaixaHoraria",
                table: "ZonasDeRisco",
                columns: new[] { "ZonaId", "FaixaHoraria" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContatosEmergencia");

            migrationBuilder.DropTable(
                name: "Ocorrencias");

            migrationBuilder.DropTable(
                name: "ZonasDeRisco");

            migrationBuilder.DropTable(
                name: "Motoboys");
        }
    }
}
