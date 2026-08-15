using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitRoute.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives seats a physical position (Row, Column) and vehicles the layout descriptor those
    /// positions are read against. Both seat columns are non-nullable on a table that already has
    /// rows, so the columns land with a placeholder default and are then backfilled in place
    /// before the uniqueness index is created.
    /// <para>
    /// Backfill rules: a label shaped like "12B" carries its own position — the digits are the row
    /// and the letter is the seat within the row, shifted one column to the right once it passes
    /// the aisle (A,B -> columns 1,2 and C,D -> columns 4,5, leaving column 3 as the aisle). Any
    /// other label (e.g. "S1") carries no position at all, so those seats are simply stacked one
    /// per row in column 1, starting below the last row the labelled seats claimed, which keeps
    /// every (VehicleId, Row, Column) distinct. Vehicle layout is then derived from the widest and
    /// deepest seat each vehicle ended up with.
    /// </para>
    /// <para>
    /// This runs on PostgreSQL only: SQLite hosts build their schema with EnsureCreated and never
    /// execute migrations.
    /// </para>
    /// </summary>
    public partial class AddSeatPositions : Migration
    {
        // Deliberately '[1-9][0-9]?' rather than '\d{1,2}': a label like "0A" would derive row 0,
        // which the domain rejects, so it falls through to the sequential branch instead.
        private const string LabelPattern = "^[1-9][0-9]?[A-Z]$";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AisleAfterColumn",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowCount",
                table: "vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeatsPerRow",
                table: "vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Column",
                table: "seats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Row",
                table: "seats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 1. Seats whose label already encodes a position.
            migrationBuilder.Sql($"""
                UPDATE seats
                SET "Row" = CAST(substring("Number" from '^([1-9][0-9]?)[A-Z]$') AS integer),
                    "Column" = CASE
                        WHEN ascii(substring("Number" from '^[1-9][0-9]?([A-Z])$')) - 64 > 2
                            THEN ascii(substring("Number" from '^[1-9][0-9]?([A-Z])$')) - 64 + 1
                        ELSE ascii(substring("Number" from '^[1-9][0-9]?([A-Z])$')) - 64
                    END
                WHERE "Number" ~ '{LabelPattern}';
                """);

            // 2. Everything else: one seat per row in column 1, stacked below the rows the
            //    labelled seats on the same vehicle already occupy.
            migrationBuilder.Sql($"""
                WITH labelled AS (
                    SELECT "VehicleId", MAX("Row") AS max_row
                    FROM seats
                    WHERE "Number" ~ '{LabelPattern}'
                    GROUP BY "VehicleId"
                ),
                sequential AS (
                    SELECT s."Id",
                           COALESCE(l.max_row, 0)
                             + ROW_NUMBER() OVER (PARTITION BY s."VehicleId" ORDER BY s."Number") AS new_row
                    FROM seats s
                    LEFT JOIN labelled l ON l."VehicleId" = s."VehicleId"
                    WHERE s."Number" !~ '{LabelPattern}'
                )
                UPDATE seats s
                SET "Row" = sequential.new_row,
                    "Column" = 1
                FROM sequential
                WHERE s."Id" = sequential."Id";
                """);

            // 3. Derive each vehicle's layout from the seats it actually has. A cabin more than
            //    two columns wide only got that way from the aisle shift in step 1, so its aisle
            //    sits after column 2; anything narrower has no aisle to speak of.
            migrationBuilder.Sql("""
                UPDATE vehicles v
                SET "RowCount" = extent.max_row,
                    "SeatsPerRow" = extent.max_column,
                    "AisleAfterColumn" = CASE WHEN extent.max_column > 2 THEN 2 ELSE NULL END
                FROM (
                    SELECT "VehicleId", MAX("Row") AS max_row, MAX("Column") AS max_column
                    FROM seats
                    GROUP BY "VehicleId"
                ) AS extent
                WHERE v."Id" = extent."VehicleId";
                """);

            // 4. Safety net for any vehicle row that has no seats at all, so the non-nullable
            //    layout columns never hold a value the domain would reject.
            migrationBuilder.Sql("""
                UPDATE vehicles
                SET "RowCount" = GREATEST("RowCount", 1),
                    "SeatsPerRow" = GREATEST("SeatsPerRow", 1)
                WHERE "RowCount" < 1 OR "SeatsPerRow" < 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_seats_VehicleId_Row_Column",
                table: "seats",
                columns: new[] { "VehicleId", "Row", "Column" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_seats_VehicleId_Row_Column",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "AisleAfterColumn",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "RowCount",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "SeatsPerRow",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Column",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "Row",
                table: "seats");
        }
    }
}
