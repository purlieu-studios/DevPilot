namespace API.Services;

/// <summary>
/// Handles user-related operations for the API project.
/// </summary>
public class UserService
{
    /// <summary>
    /// Gets a user by their ID.
    /// </summary>
    public string GetUser(int userId)
    {
        return $"User {userId}";
    }
}
