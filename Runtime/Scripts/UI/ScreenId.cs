using System;

namespace SimpleUIScreensSystem
{
    /// <summary>A stable catalog key, independent of its C# name and prefab address.</summary>
    public readonly struct ScreenId : IEquatable<ScreenId>
    {
        private readonly string _value;

        public string Value => _value ?? string.Empty;
        public bool IsValid => _value != null;

        public ScreenId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Screen ID must be nonempty.", nameof(value));
            _value = value;
        }

        /// <summary>Converts an inspector string. A blank string yields an invalid ID instead of throwing.</summary>
        public static ScreenId FromSerialized(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? default : new ScreenId(value);
        }

        public bool Equals(ScreenId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ScreenId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public override string ToString() => Value;
        public static bool operator ==(ScreenId left, ScreenId right) => left.Equals(right);
        public static bool operator !=(ScreenId left, ScreenId right) => !left.Equals(right);
    }
}
