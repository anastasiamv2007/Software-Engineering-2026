using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql; // Або інша бібліотека, яку ти використовуєш
using SoftwareEngineering.DAL.Entities;



namespace SoftwareEngineering.DAL.Repositories
{
    public class SubtaskRepository : ISubtaskRepository
    {
        private readonly string _connectionString;

        public SubtaskRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<SubtaskEntity> AddAsync(SubtaskEntity entity)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "INSERT INTO Subtasks (Id, TaskId, Title, IsCompleted) VALUES (@Id, @TaskId, @Title, @IsCompleted)";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Id", entity.Id);
            command.Parameters.AddWithValue("TaskId", entity.TaskId);
            command.Parameters.AddWithValue("Title", entity.Title);
            command.Parameters.AddWithValue("IsCompleted", entity.IsCompleted);

            await command.ExecuteNonQueryAsync();
            return entity;
        }

        public async Task UpdateStatusAsync(Guid id, bool isCompleted)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Змінюємо ТІЛЬКИ статус (True/False). Назву не чіпаємо.
            var query = "UPDATE Subtasks SET IsCompleted = @IsCompleted WHERE Id = @Id";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Id", id);
            command.Parameters.AddWithValue("IsCompleted", isCompleted);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM Subtasks WHERE Id = @Id";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Id", id);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<SubtaskEntity>> GetByTaskIdAsync(Guid taskId)
        {
            var result = new List<SubtaskEntity>();
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT Id, TaskId, Title, IsCompleted FROM Subtasks WHERE TaskId = @TaskId ORDER BY Title";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("TaskId", taskId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SubtaskEntity
                {
                    Id = reader.GetGuid(0),
                    TaskId = reader.GetGuid(1),
                    Title = reader.GetString(2),
                    IsCompleted = reader.GetBoolean(3)
                });
            }

            return result;
        }
    }
}