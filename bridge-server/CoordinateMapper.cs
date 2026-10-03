using System.Text.Json;

namespace MC7DTD.Bridge;

/// <summary>Maps Minecraft coordinates to 7DTD coordinates: output = input * scale + offset.</summary>
public sealed class CoordinateMapper
{
    public double Scale { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }
    public double OffsetZ { get; }

    public CoordinateMapper(double scale, double offsetX, double offsetY, double offsetZ)
    {
        if (!double.IsFinite(scale) || !double.IsFinite(offsetX) ||
            !double.IsFinite(offsetY) || !double.IsFinite(offsetZ))
            throw new InvalidDataException("Coordinate mapping values must be finite numbers.");
        (Scale, OffsetX, OffsetY, OffsetZ) = (scale, offsetX, offsetY, offsetZ);
    }

    public static CoordinateMapper Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        return new CoordinateMapper(Read("scale"), Read("offsetX"), Read("offsetY"), Read("offsetZ"));

        double Read(string name)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) ||
                !double.IsFinite(number))
                throw new InvalidDataException($"coordinate.json requires a finite numeric {name}.");
            return number;
        }
    }

    public bool TryMap(double x, double y, double z, out MappedPosition position)
    {
        position = new MappedPosition(x * Scale + OffsetX, y * Scale + OffsetY, z * Scale + OffsetZ);
        return double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z) &&
            double.IsFinite(position.X) && double.IsFinite(position.Y) && double.IsFinite(position.Z);
    }
}

public readonly record struct MappedPosition(double X, double Y, double Z);
