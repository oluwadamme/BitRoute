namespace BitRoute.Domain.Enums;

/// <summary>
/// The roles a user account can hold. This is the canonical, framework-free list.
/// Infrastructure maps these names onto ASP.NET Core Identity roles for authorization,
/// and other layers reference a user only by its id, never by an Identity type.
/// Passengers self-register; operators and admins are provisioned.
/// </summary>
public enum UserRole
{
    Passenger = 0,
    Operator = 1,
    Admin = 2
}
