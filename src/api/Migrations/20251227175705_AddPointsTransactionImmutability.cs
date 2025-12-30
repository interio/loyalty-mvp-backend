using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPointsTransactionImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.prevent_points_transactions_update_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  RAISE EXCEPTION 'PointsTransactions is an immutable ledger (UPDATE/DELETE is not allowed). Use an insert-based compensating transaction instead.';
END;
$$;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_trigger
    WHERE tgname = 'trg_points_transactions_immutable'
  ) THEN
    CREATE TRIGGER trg_points_transactions_immutable
    BEFORE UPDATE OR DELETE ON public.""PointsTransactions""
    FOR EACH ROW
    EXECUTE FUNCTION public.prevent_points_transactions_update_delete();
  END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_points_transactions_immutable ON public.""PointsTransactions"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.prevent_points_transactions_update_delete();");
        }
    }
}
