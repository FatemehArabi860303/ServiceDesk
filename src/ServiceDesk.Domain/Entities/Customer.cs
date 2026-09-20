namespace ServiceDesk.Domain.Entities;

public sealed class Customer
{
    public const int FirstNameMaxLength = 100;
    public const int LastNameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 30;

    private Customer(Guid id, string firstName, string lastName, string email, string? phone, DateTimeOffset now)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public string? Phone { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Customer Create(string firstName, string lastName, string email, string? phone = null)
    {
        ValidateRequired(firstName, nameof(firstName), FirstNameMaxLength);
        ValidateRequired(lastName, nameof(lastName), LastNameMaxLength);
        ValidateRequired(email, nameof(email), EmailMaxLength);
        ValidateOptional(phone, nameof(phone), PhoneMaxLength);

        return new Customer(Guid.NewGuid(), firstName.Trim(), lastName.Trim(), email.Trim(), phone?.Trim(), DateTimeOffset.UtcNow);
    }

    public void UpdateContactInformation(string firstName, string lastName, string email, string? phone = null)
    {
        ValidateRequired(firstName, nameof(firstName), FirstNameMaxLength);
        ValidateRequired(lastName, nameof(lastName), LastNameMaxLength);
        ValidateRequired(email, nameof(email), EmailMaxLength);
        ValidateOptional(phone, nameof(phone), PhoneMaxLength);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim();
        Phone = phone?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }
    }

    private static void ValidateOptional(string? value, string parameterName, int maxLength)
    {
        if (value is not null && value.Trim().Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }
    }
}
