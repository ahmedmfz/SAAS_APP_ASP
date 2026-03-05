using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotSubscriptionLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApiCallsMonthly",
                table: "OrganizationSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ApiCallsPerUser",
                table: "OrganizationSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiCallsMonthly",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "ApiCallsPerUser",
                table: "OrganizationSubscriptions");
        }
    }
}
