namespace RecipeHub.Domain.Entities;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public required string CreatorId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string HttpMethod { get; set; }
    public required string Path { get; set; }
    public int StatusCode { get; set; }
    public required string ResponseBody { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
