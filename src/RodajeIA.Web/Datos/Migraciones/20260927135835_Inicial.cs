using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RodajeIA.Web.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Personajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SerieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Edad = table.Column<string>(type: "TEXT", nullable: false),
                    DescripcionFisica = table.Column<string>(type: "TEXT", nullable: false),
                    Peinado = table.Column<string>(type: "TEXT", nullable: false),
                    Vestuario = table.Column<string>(type: "TEXT", nullable: false),
                    Heridas = table.Column<string>(type: "TEXT", nullable: true),
                    Personalidad = table.Column<string>(type: "TEXT", nullable: false),
                    Rol = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personajes_Series_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Personajes_SerieId_Nombre",
                table: "Personajes",
                columns: new[] { "SerieId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Personajes");

            migrationBuilder.DropTable(
                name: "Series");
        }
    }
}
