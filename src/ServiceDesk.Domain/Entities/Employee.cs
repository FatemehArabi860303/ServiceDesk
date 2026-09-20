namespace ServiceDesk.Domain.Entities;

public sealed class Employee
{
    public const int FirstNameMaxLength = 100;
    public const int LastNameMaxLength = 100;
    public const int EmailMaxLength = 254;

    private Employee(Guid id, string firstName, string lastName, string email, DateTimeOffset now)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Employee Create(string firstName, string lastName, string email)
    {
        ValidateRequired(firstName, nameof(firstName), FirstNameMaxLength);
        ValidateRequired(lastName, nameof(lastName), LastNameMaxLength);
        ValidateRequired(email, nameof(email), EmailMaxLength);

        return new Employee(Guid.NewGuid(), firstName.Trim(), lastName.Trim(), email.Trim(), DateTimeOffset.UtcNow);
    }

    public void UpdateProfile(string firstName, string lastName, string email)
    {
        ValidateRequired(firstName, nameof(firstName), FirstNameMaxLength);
        ValidateRequired(lastName, nameof(lastName), LastNameMaxLength);
        ValidateRequired(email, nameof(email), EmailMaxLength);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
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
}
