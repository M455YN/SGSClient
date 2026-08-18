namespace SGSClient.Core.Enums;

/// <summary>Account permission level. Higher values include everything below them.</summary>
public enum UserRole
{
    /// <summary>Plays and buys games.</summary>
    User = 0,

    /// <summary>Everything a user can do, plus publishing own games.</summary>
    Creator = 1,

    /// <summary>Manages the catalog, curation and user roles.</summary>
    Admin = 2,

    /// <summary>Admin who may also grant or revoke admin roles.</summary>
    SuperAdmin = 3
}
