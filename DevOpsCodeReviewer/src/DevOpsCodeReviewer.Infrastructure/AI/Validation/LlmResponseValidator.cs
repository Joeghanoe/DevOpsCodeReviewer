using Microsoft.Extensions.Logging;

namespace DevOpsCodeReviewer.Infrastructure.AI.Validation;

/// <summary>
/// Shared validation helpers for LLM response fields.
/// </summary>
public static class LlmResponseValidator
{
    /// <summary>
    /// Clamps a nullable integer to a valid range.
    /// </summary>
    /// <param name="value">The nullable value to clamp.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <param name="defaultValue">Default value if null.</param>
    /// <param name="fieldName">Field name for logging.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>The clamped value.</returns>
    public static int ClampScore(int? value, int min, int max, int defaultValue, string fieldName, ILogger logger)
    {
        if (value == null)
        {
            logger.LogDebug("{Field} was null, defaulting to {Default}", fieldName, defaultValue);
            return defaultValue;
        }

        if (value < min)
        {
            logger.LogWarning("{Field} was {Value}, clamping to {Min}", fieldName, value, min);
            return min;
        }

        if (value > max)
        {
            logger.LogWarning("{Field} was {Value}, clamping to {Max}", fieldName, value, max);
            return max;
        }

        return value.Value;
    }

    /// <summary>
    /// Returns the string value or empty string if null/whitespace.
    /// Logs a warning if the field was expected to have a value.
    /// </summary>
    /// <param name="value">The nullable string value.</param>
    /// <param name="fieldName">Field name for logging.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="required">Whether the field is required (logs warning if missing).</param>
    /// <returns>The string value or empty string.</returns>
    public static string GetRequiredString(string? value, string fieldName, ILogger logger, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                logger.LogWarning("{Field} was null or empty", fieldName);
            }
            return string.Empty;
        }
        return value;
    }

    /// <summary>
    /// Validates and normalizes an enum-like string value.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="validValues">Set of valid values (case-insensitive).</param>
    /// <param name="defaultValue">Default value if invalid.</param>
    /// <param name="fieldName">Field name for logging.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>The normalized value or default.</returns>
    public static string ValidateEnumString(
        string? value,
        HashSet<string> validValues,
        string defaultValue,
        string fieldName,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            logger.LogDebug("{Field} was null or empty, defaulting to {Default}", fieldName, defaultValue);
            return defaultValue;
        }

        var normalized = value.ToLowerInvariant().Trim();

        if (validValues.Contains(normalized))
        {
            return normalized;
        }

        logger.LogWarning("{Field} value '{Value}' is not valid, defaulting to {Default}",
            fieldName, value, defaultValue);
        return defaultValue;
    }

    /// <summary>
    /// Checks if a required field is missing and adds an error to the list.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="fieldName">Field name for error message.</param>
    /// <param name="errors">List to add errors to.</param>
    /// <returns>True if the field is valid (not missing), false if missing.</returns>
    public static bool RequireNonEmpty(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Missing required '{fieldName}' field");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if a required integer field is missing or invalid.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="minValue">Minimum valid value.</param>
    /// <param name="fieldName">Field name for error message.</param>
    /// <param name="errors">List to add errors to.</param>
    /// <returns>True if the field is valid, false if missing or invalid.</returns>
    public static bool RequireValidInt(int? value, int minValue, string fieldName, List<string> errors)
    {
        if (value == null)
        {
            errors.Add($"Missing required '{fieldName}' field");
            return false;
        }

        if (value < minValue)
        {
            errors.Add($"'{fieldName}' must be >= {minValue} (got {value})");
            return false;
        }

        return true;
    }
}
