using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitRoute.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRelationalEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stops_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartureTimeOfDay = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_schedules_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schedules_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seats_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schedule_legs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartStopIndex = table.Column<int>(type: "integer", nullable: false),
                    EndStopIndex = table.Column<int>(type: "integer", nullable: false),
                    Fare = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_legs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_schedule_legs_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seat_bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PassengerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardingIndex = table.Column<int>(type: "integer", nullable: false),
                    AlightingIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    HoldExpiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seat_bookings_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seat_bookings_seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_legs_ScheduleId_StartStopIndex_EndStopIndex",
                table: "schedule_legs",
                columns: new[] { "ScheduleId", "StartStopIndex", "EndStopIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedules_RouteId",
                table: "schedules",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_schedules_VehicleId",
                table: "schedules",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_bookings_IdempotencyKey",
                table: "seat_bookings",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seat_bookings_ScheduleId_TravelDate_SeatId_Status",
                table: "seat_bookings",
                columns: new[] { "ScheduleId", "TravelDate", "SeatId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_seat_bookings_SeatId",
                table: "seat_bookings",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_seats_VehicleId_Number",
                table: "seats",
                columns: new[] { "VehicleId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stops_RouteId_Index",
                table: "stops",
                columns: new[] { "RouteId", "Index" },
                unique: true);

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
                migrationBuilder.Sql(@"
                    ALTER TABLE seat_bookings
                    ADD CONSTRAINT no_overlapping_legs
                    EXCLUDE USING gist (
                      ""ScheduleId"" WITH =,
                      ""TravelDate"" WITH =,
                      ""SeatId""     WITH =,
                      int4range(""BoardingIndex"", ""AlightingIndex"", '[)') WITH &&
                    ) WHERE (""Status"" IN ('Held', 'Confirmed'));
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("ALTER TABLE seat_bookings DROP CONSTRAINT IF EXISTS no_overlapping_legs;");
            }

            migrationBuilder.DropTable(
                name: "schedule_legs");

            migrationBuilder.DropTable(
                name: "seat_bookings");

            migrationBuilder.DropTable(
                name: "stops");

            migrationBuilder.DropTable(
                name: "schedules");

            migrationBuilder.DropTable(
                name: "seats");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
