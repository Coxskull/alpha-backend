using Alpha.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/database-diagnostic")]
public class DatabaseDiagnosticController
    : ControllerBase
{
    private readonly AppDbContext _context;

    public DatabaseDiagnosticController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var connection =
            (NpgsqlConnection)
            _context.Database.GetDbConnection();

        var shouldClose =
            connection.State !=
            System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(
                cancellationToken
            );
        }

        try
        {
            await using var command =
                connection.CreateCommand();

            command.CommandText = """
                SELECT
                    current_database(),
                    current_schema(),
                    current_user,
                    inet_server_addr()::text,
                    EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'payments'
                          AND column_name = 'checkout_url'
                    );
                """;

            await using var reader =
                await command.ExecuteReaderAsync(
                    cancellationToken
                );

            await reader.ReadAsync(
                cancellationToken
            );

            return Ok(new
            {
                database =
                    reader.GetString(0),

                schema =
                    reader.IsDBNull(1)
                        ? null
                        : reader.GetString(1),

                databaseUser =
                    reader.GetString(2),

                serverAddress =
                    reader.IsDBNull(3)
                        ? null
                        : reader.GetString(3),

                checkoutUrlExists =
                    reader.GetBoolean(4)
            });
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}