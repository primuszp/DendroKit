namespace DendroKit.Core.Geom;

public sealed class Vector3d
{
    public static readonly Vector3d XAxis = new(1, 0, 0);
    public static readonly Vector3d YAxis = new(0, 1, 0);
    public static readonly Vector3d ZAxis = new(0, 0, 1);
    public static readonly Vector3d Zero  = new(0, 0, 0);

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Vector3d() { }
    public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
    public Vector3d(double v) { X = Y = Z = v; }
    public Vector3d(Vector3d v) { X = v.X; Y = v.Y; Z = v.Z; }

    public double Abs() => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vector3d Normalize()
    {
        double a = Abs();
        return new Vector3d(X / a, Y / a, Z / a);
    }

    public Vector3d Mul(double f) => new(X * f, Y * f, Z * f);
    public Vector3d Div(double f) => Mul(1.0 / f);
    public Vector3d Add(Vector3d v) => new(X + v.X, Y + v.Y, Z + v.Z);
    public Vector3d Sub(Vector3d v) => Add(v.Mul(-1));
    public double Dot(Vector3d v) => X * v.X + Y * v.Y + Z * v.Z;

    public void SetMaxCoord(ref double mx, ref double my, ref double mz)
    {
        if (X > mx) mx = X;
        if (Y > my) my = Y;
        if (Z > mz) mz = Z;
    }
    public void SetMinCoord(ref double mx, ref double my, ref double mz)
    {
        if (X < mx) mx = X;
        if (Y < my) my = Y;
        if (Z < mz) mz = Z;
    }

    public bool ApproxEquals(Vector3d v) => Sub(v).Abs() < 1e-7;

    public static double Atan2Deg(double v, double u)
    {
        if (u == 0) return v >= 0 ? 90 : -90;
        if (u > 0)  return Math.Atan(v / u) * 180 / Math.PI;
        if (v >= 0) return 180 + Math.Atan(v / u) * 180 / Math.PI;
        return Math.Atan(v / u) * 180 / Math.PI - 180;
    }

    public override string ToString() =>
        $"<{X:F6},{Y:F6},{Z:F6}>";
}
