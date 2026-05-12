namespace DendroKit.Core.Geom;

public sealed class Matrix3
{
    private readonly double[] _d = new double[9]; // row-major [row*3+col]

    public Matrix3()
    {
        // identity
        _d[0] = _d[4] = _d[8] = 1.0;
    }

    public Matrix3(double xx, double xy, double xz,
                   double yx, double yy, double yz,
                   double zx, double zy, double zz)
    {
        _d[0]=xx; _d[1]=xy; _d[2]=xz;
        _d[3]=yx; _d[4]=yy; _d[5]=yz;
        _d[6]=zx; _d[7]=zy; _d[8]=zz;
    }

    private Matrix3(double[] src)
    {
        Array.Copy(src, _d, 9);
    }

    public double Get(int r, int c) => _d[r * 3 + c];

    public Vector3d Row(int r) => new(_d[r*3], _d[r*3+1], _d[r*3+2]);
    public Vector3d Col(int c) => new(_d[c], _d[3+c], _d[6+c]);

    public Matrix3 Transpose()
    {
        var t = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                t[r*3+c] = _d[c*3+r];
        return new Matrix3(t);
    }

    public Matrix3 Mul(double f)
    {
        var r = new double[9];
        for (int i = 0; i < 9; i++) r[i] = _d[i] * f;
        return new Matrix3(r);
    }

    public Matrix3 Prod(Matrix3 m)
    {
        var r = new double[9];
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                r[row*3+col] = Row(row).Dot(m.Col(col));
        return new Matrix3(r);
    }

    public Vector3d Prod(Vector3d v) =>
        new(Row(0).Dot(v), Row(1).Dot(v), Row(2).Dot(v));

    public Matrix3 Add(Matrix3 m)
    {
        var r = new double[9];
        for (int i = 0; i < 9; i++) r[i] = _d[i] + m._d[i];
        return new Matrix3(r);
    }

    public Matrix3 Sub(Matrix3 m) => Add(m.Mul(-1));
    public Matrix3 Div(double f) => Mul(1.0 / f);

    public Vector3d GetScale() => new(
        Math.Sqrt(_d[0]*_d[0] + _d[1]*_d[1] + _d[2]*_d[2]),
        Math.Sqrt(_d[3]*_d[3] + _d[4]*_d[4] + _d[5]*_d[5]),
        Math.Sqrt(_d[6]*_d[6] + _d[7]*_d[7] + _d[8]*_d[8]));

    public override string ToString() => $"x:{Row(0)} y:{Row(1)} z:{Row(2)}";
}
