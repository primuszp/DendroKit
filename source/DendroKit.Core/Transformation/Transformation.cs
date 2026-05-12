namespace DendroKit.Core.Geom;

public sealed class Transformation
{
    public Matrix3  Matrix { get; }
    public Vector3d Vector { get; }

    public Transformation()
    {
        Matrix = new Matrix3();
        Vector = new Vector3d();
    }

    public Transformation(Matrix3 m, Vector3d v)
    {
        Matrix = m;
        Vector = v;
    }

    public Vector3d GetX() => Matrix.Col(0);
    public Vector3d GetY() => Matrix.Col(1);
    public Vector3d GetZ() => Matrix.Col(2);
    public Vector3d GetT() => Vector;

    public Transformation Prod(Transformation t1) =>
        new(Matrix.Prod(t1.Matrix), Matrix.Prod(t1.Vector).Add(Vector));

    public Vector3d Apply(Vector3d v)           => Matrix.Prod(v).Add(Vector);
    public Vector3d ApplyRotation(Vector3d v)   => Matrix.Prod(v);

    public Transformation Translate(Vector3d v) =>
        new(Matrix, Vector.Add(v));

    public Transformation RotZ(double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180;
        var rm = new Matrix3(Math.Cos(rad), -Math.Sin(rad), 0,
                             Math.Sin(rad),  Math.Cos(rad), 0,
                             0, 0, 1);
        return new Transformation(Matrix.Prod(rm), Vector);
    }

    public Transformation RotY(double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180;
        var rm = new Matrix3( Math.Cos(rad), 0, -Math.Sin(rad),
                              0, 1, 0,
                              Math.Sin(rad), 0,  Math.Cos(rad));
        return new Transformation(Matrix.Prod(rm), Vector);
    }

    public Transformation RotX(double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180;
        var rm = new Matrix3(1, 0, 0,
                             0,  Math.Cos(rad), -Math.Sin(rad),
                             0,  Math.Sin(rad),  Math.Cos(rad));
        return new Transformation(Matrix.Prod(rm), Vector);
    }

    public Transformation RotXZ(double delta, double rho)
    {
        double rd = delta * Math.PI / 180;
        double rr = rho   * Math.PI / 180;
        double sir = Math.Sin(rr), cor = Math.Cos(rr);
        double sid = Math.Sin(rd), cod = Math.Cos(rd);
        var rm = new Matrix3(cor,  -sir*cod, sir*sid,
                             sir,   cor*cod,-cor*sid,
                             0,     sid,     cod);
        return new Transformation(Matrix.Prod(rm), Vector);
    }

    public Transformation RotAxisZ(double delta, double rho)
    {
        double rd = delta * Math.PI / 180;
        double rr = rho   * Math.PI / 180;
        double a = Math.Cos(rr), b = Math.Sin(rr);
        double si = Math.Sin(rd), co = Math.Cos(rd);
        var rm = new Matrix3(
            co + a*a*(1-co),  b*a*(1-co),   b*si,
            a*b*(1-co),       co+b*b*(1-co),-a*si,
            -b*si,            a*si,          co);
        return new Transformation(Matrix.Prod(rm), Vector);
    }

    public Transformation RotAxis(double angleDeg, Vector3d axis)
    {
        double rad = angleDeg * Math.PI / 180;
        var n = axis.Normalize();
        double a = n.X, b = n.Y, c = n.Z;
        double si = Math.Sin(rad), co = Math.Cos(rad);
        var rm = new Matrix3(
             co+a*a*(1-co),    -c*si+b*a*(1-co),  b*si+c*a*(1-co),
             c*si+a*b*(1-co),   co+b*b*(1-co),    -a*si+c*b*(1-co),
            -b*si+a*c*(1-co),   a*si+b*c*(1-co),   co+c*c*(1-co));
        return new Transformation(rm.Prod(Matrix), Vector);
    }

    public Transformation Inverse()
    {
        var t = Matrix.Transpose();
        return new Transformation(t, t.Prod(Vector.Mul(-1)));
    }

    public override string ToString() =>
        $"x:{GetX()} y:{GetY()} z:{GetZ()} t:{GetT()}";
}
