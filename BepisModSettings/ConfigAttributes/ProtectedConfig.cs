namespace BepisModSettings.ConfigAttributes;

public class ProtectedConfig(string maskString)
{
    public ProtectedConfig() : this("*") { }

    public string MaskString { get; } = maskString;
}