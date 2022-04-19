namespace System;

public static class StringExtensions
{
    public static bool EqualsOrdinal(this string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);

    public static bool EqualsOrdinalIgnoreCase(this string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public static bool IsNotNullOrEmpty(this string? value) => !string.IsNullOrEmpty(value);

    public static bool IsNotNullOrWhiteSpace(this string? value) => !string.IsNullOrWhiteSpace(value);

    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);

    public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);
}
