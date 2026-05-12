using DendroKit.Core.Mesh;
using DendroKit.Core.Tree;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace DendroKit.View;

/// <summary>
/// OpenGL 3D viewport with Hilton 7-parameter camera.
/// Left-drag = orbit  |  Right-drag = pan  |  Wheel = zoom-to-cursor  |  Space = perspective/ortho
/// </summary>
public sealed class TreeGLControl : UserControl
{
    // ── GL ─────────────────────────────────────────────────────────────
    private GLControl? _gl;
    private bool       _glReady;

    // ── tree data ───────────────────────────────────────────────────────
    private TreeMesh?            _mesh;
    private readonly List<LeafQ> _leaves = new();
    private float _leafW, _leafL, _leafStem;
    private int   _leafList  = 0;
    private bool  _leafDirty = false;
    private float _treeH     = 5f;

    // ── Hilton camera ──────────────────────────────────────────────────
    private float  _hw, _hh;
    private float  _zn  =  1000f;
    private float  _zf  = -1000f;
    private float  _iez =  0f;
    private float  _tsx =  0f;
    private float  _tsy =  0f;

    private enum ProjMode { Perspective, Orthographic }
    private ProjMode _proj      = ProjMode.Perspective;
    private double   _viewAngle = 45.0 * Math.PI / 180.0;
    private double   _halfView  = 10.0;

    private Matrix4 _v2wRot = Matrix4.Identity;
    private Vector3 _v2wTrn = Vector3.Zero;
    private float   _azimuth   = 30f;
    private float   _elevation = 25f;

    // depth-pick state (view space)
    private Vector3 _pickedPtView = Vector3.Zero;
    private float   _pickedDepth  = 1.0f;

    // orbit pivot in world (Z-up)
    private Vector3 _pivot = new(0, 0, 0);

    // ── input ──────────────────────────────────────────────────────────
    private Point _lastMouse;
    private bool  _lDown, _rDown;

    // ── colors ─────────────────────────────────────────────────────────
    private static readonly (float R, float G, float B)[] Bark =
    {
        (0.40f, 0.29f, 0.18f),
        (0.34f, 0.25f, 0.16f),
        (0.29f, 0.21f, 0.14f),
        (0.24f, 0.18f, 0.12f),
    };

    private static readonly float[] BarkSpecular = [0.10f, 0.09f, 0.08f, 1f];
    private static readonly float[] LeafSpecular = [0.12f, 0.14f, 0.10f, 1f];
    private static readonly float[] NoEmission   = [0f, 0f, 0f, 1f];

    public TreeGLControl() => Dock = DockStyle.Fill;

    // ── lifecycle ──────────────────────────────────────────────────────

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _gl = new GLControl(new GLControlSettings
        {
            Profile         = ContextProfile.Compatability,
            DepthBits       = 24,
            StencilBits     = 0,
            NumberOfSamples = 0,
            IsEventDriven   = true,
        })
        { Dock = DockStyle.Fill };

        _gl.Load       += OnLoad;
        _gl.Paint      += OnPaint;
        _gl.Resize     += OnResize;
        _gl.MouseDown  += OnMouseDown;
        _gl.MouseUp    += OnMouseUp;
        _gl.MouseMove  += OnMouseMove;
        _gl.MouseWheel += OnMouseWheel;
        _gl.KeyDown    += OnKeyDown;
        Controls.Add(_gl);
    }

    // ── public ─────────────────────────────────────────────────────────

    public void SetTree(ITree? tree, TreeMesh? mesh, bool resetCamera = false)
    {
        if (InvokeRequired) { Invoke(() => SetTree(tree, mesh, resetCamera)); return; }

        _mesh = mesh;
        _leaves.Clear();

        if (tree != null)
        {
            _leafW    = (float)tree.LeafWidth;
            _leafL    = (float)tree.LeafLength;
            // Negative LeafStemLen means "relative to parent radius" in Arbaro — use 0 for display
            _leafStem = (float)Math.Max(0.0, tree.LeafStemLength);
            tree.TraverseTree(new LeafCollector(_leaves));
        }

        float newH = CalcHeight(mesh);
        if (resetCamera || _treeH < 0.001f)
        {
            _treeH    = newH;
            _halfView = _treeH;
            _pivot    = new Vector3(0, 0, _treeH * 0.4f);
            _v2wTrn   = Vector3.Zero;
            BuildRotation();
        }
        else
        {
            _treeH = newH;
        }
        _leafDirty = true;

        if (_glReady)
        {
            _gl!.MakeCurrent();
            CalcViewVolume(_gl.Width, _gl.Height);
        }
        _gl?.Refresh();   // Invalidate + Update: forces immediate WM_PAINT in WindowsFormsHost
    }

    public void SetMesh(TreeMesh? mesh) => SetTree(null, mesh, resetCamera: true);

    // ── GL init ────────────────────────────────────────────────────────

    private void OnLoad(object? sender, EventArgs e)
    {
        try { _gl!.MakeCurrent(); DoGLInit(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("GL Load: " + ex); }
    }

    private void DoGLInit()
    {
        GL.ClearColor(0.988f, 0.973f, 0.922f, 1f);   // warm ivory
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Normalize);
        GL.Enable(EnableCap.Lighting);
        GL.Enable(EnableCap.Light0);
        GL.Enable(EnableCap.Light1);
        GL.Enable(EnableCap.ColorMaterial);
        GL.ColorMaterial(TriangleFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
        GL.ShadeModel(ShadingModel.Smooth);
        GL.LightModel(LightModelParameter.LightModelTwoSide, 1);
        GL.Light(LightName.Light0, LightParameter.Diffuse,  new[] { 0.94f, 0.95f, 0.93f, 1f });
        GL.Light(LightName.Light0, LightParameter.Ambient,  new[] { 0.22f, 0.22f, 0.22f, 1f });
        GL.Light(LightName.Light0, LightParameter.Specular, new[] { 0.20f, 0.20f, 0.18f, 1f });
        GL.Light(LightName.Light1, LightParameter.Diffuse,  new[] { 0.24f, 0.27f, 0.24f, 1f });
        GL.Light(LightName.Light1, LightParameter.Ambient,  new[] { 0.04f, 0.05f, 0.04f, 1f });
        GL.Light(LightName.Light1, LightParameter.Specular, new[] { 0.03f, 0.03f, 0.03f, 1f });
        GL.Material(TriangleFace.FrontAndBack, MaterialParameter.Emission, NoEmission);

        BuildRotation();
        CalcViewVolume(_gl!.Width, _gl.Height);
        GL.Viewport(0, 0, _gl.Width, _gl.Height);
        _glReady = true;
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (!_glReady) return;
        _gl!.MakeCurrent();
        GL.Viewport(0, 0, _gl.Width, _gl.Height);
        CalcViewVolume(_gl.Width, _gl.Height);
        _gl.Refresh();
    }

    // ── Hilton camera ──────────────────────────────────────────────────

    /// Recompute hw/hh/iez/_zn/_zf from _halfView and current aspect.
    private void CalcViewVolume(int w, int h)
    {
        _hw = _hh = (float)_halfView;
        if (w >= h) _hw *= (float)w / h;
        else        _hh *= (float)h / w;

        if (_proj == ProjMode.Orthographic || _viewAngle == 0)
        {
            _iez = 0f;
            // symmetric depth range for ortho, scales with zoom
            float half = (float)_halfView * 6f;
            _zn =  half;
            _zf = -half;
        }
        else
        {
            float ez = Math.Min(_hw, _hh) / (float)Math.Tan(0.5 * _viewAngle);
            _iez = 1f / ez;
            _zn  = ez - 1e-3f;              // just inside the eye
            _zf  = -(ez * 2f + _treeH * 3f); // well behind the scene
        }
    }

    /// Load Hilton projection + modelview into GL.
    private void ApplyProjection()
    {
        Matrix4 proj = new();

        if (_proj == ProjMode.Orthographic)
        {
            proj.Row0 = new Vector4(1f / _hw, 0, 0, 0);
            proj.Row1 = new Vector4(0, 1f / _hh, 0, 0);
            proj.Row2 = new Vector4(-_tsx / _hw, -_tsy / _hh, -2f / (_zn - _zf), 0);
            proj.Row3 = new Vector4(0, 0, (_zn + _zf) / (_zn - _zf), 1f);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref proj);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        }
        else
        {
            if (Math.Abs(_iez) < 1e-6f) _iez = 1e-6f;
            float ez = 1f / _iez;

            proj.Row0 = new Vector4(ez / _hw, 0, 0, 0);
            proj.Row1 = new Vector4(0, ez / _hh, 0, 0);
            proj.Row2 = new Vector4(-ez * _tsx / _hw, -ez * _tsy / _hh,
                                    -(2f * ez - (_zn + _zf)) / (_zn - _zf), -1f);
            proj.Row3 = new Vector4(0, 0,
                                    -2f * (ez * (ez - (_zn + _zf)) + _zn * _zf) / (_zn - _zf), 0);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref proj);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.Translate(-ez * _tsx, -ez * _tsy, -ez);
        }

        // World-to-view = transpose of _v2wRot (orthogonal → transpose = inverse)
        var w2v = new Matrix4(
            _v2wRot.M11, _v2wRot.M21, _v2wRot.M31, 0,
            _v2wRot.M12, _v2wRot.M22, _v2wRot.M32, 0,
            _v2wRot.M13, _v2wRot.M23, _v2wRot.M33, 0,
            0, 0, 0, 1);
        GL.MultMatrix(ref w2v);
        GL.Translate(-_v2wTrn.X, -_v2wTrn.Y, -_v2wTrn.Z);
    }

    private Vector3 ScreenToView(int mx, int my, float vz)
    {
        float p2v = _hw * 2f / (_gl?.Width ?? 1);
        float x = mx * p2v - _hw;
        float y = -(my * p2v - _hh);
        x += -x * vz * _iez + _tsx * vz;
        y += -y * vz * _iez + _tsy * vz;
        return new Vector3(x, y, vz);
    }

    private void PickDepth(int mx, int my)
    {
        var pix = new float[1];
        GL.ReadPixels(mx, (_gl?.Height ?? 1) - my, 1, 1,
            PixelFormat.DepthComponent, PixelType.Float, pix);
        _pickedDepth = pix[0];

        float vz;
        if (_pickedDepth >= 0.9999f)
        {
            vz = _pickedPtView.Z;
        }
        else
        {
            float m33 = -(1 - _zf * _iez) / (_zn - _zf);
            vz = (_pickedDepth + m33 * _zn) / (_pickedDepth * _iez + m33);
        }
        _pickedPtView = ScreenToView(mx, my, vz);
    }

    // Transform vector v by the rotation part of matrix m  (v^T · M)
    private static Vector3 RotMul(Vector3 v, Matrix4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33);

    private void BuildRotation()
    {
        // Z-up orbit: Rz(azimuth) × Rx(elevation)
        float az = _azimuth   * MathF.PI / 180f;
        float el = _elevation * MathF.PI / 180f;
        float cz = MathF.Cos(az), sz = MathF.Sin(az);
        float cx = MathF.Cos(el), sx = MathF.Sin(el);

        _v2wRot = new Matrix4(
             cz,      sz,     0,  0,
            -sz * cx, cz * cx, sx, 0,
             sz * sx, -cz * sx, cx, 0,
             0,        0,     0,  1);
    }

    private void Rotate(int dx, int dy)
    {
        var oldRot = _v2wRot;
        _azimuth   -= dx * 0.4f;
        _elevation  = Math.Clamp(_elevation - dy * 0.4f, -89f, 89f);
        BuildRotation();

        // Hilton pivot compensation: keep _pivot at screen centre
        var pivView = _pivot - _v2wTrn;
        var oldInv  = new Matrix4(
            oldRot.M11, oldRot.M21, oldRot.M31, 0,
            oldRot.M12, oldRot.M22, oldRot.M32, 0,
            oldRot.M13, oldRot.M23, oldRot.M33, 0,
            0, 0, 0, 1);
        var pivRS      = RotMul(pivView, oldInv);
        var pivViewNew = RotMul(pivRS, _v2wRot);
        _v2wTrn = _pivot - pivViewNew;
    }

    private void Pan(Point from, Point to)
    {
        var vFrom = ScreenToView(from.X, from.Y, _pickedPtView.Z);
        var vTo   = ScreenToView(to.X,   to.Y,   _pickedPtView.Z);
        _v2wTrn  -= RotMul(vTo - vFrom, _v2wRot);
    }

    private void Zoom(Point mouse, float factor)
    {
        var before    = ScreenToView(mouse.X, mouse.Y, _pickedPtView.Z);
        _halfView     = Math.Clamp(_halfView / factor, 0.05, 5000.0);
        CalcViewVolume(_gl!.Width, _gl.Height);
        var after     = ScreenToView(mouse.X, mouse.Y, _pickedPtView.Z);
        _v2wTrn      -= RotMul(after - before, _v2wRot);
    }

    private void ToggleProjection(Point mouse)
    {
        var before = ScreenToView(mouse.X, mouse.Y, _pickedPtView.Z);
        _proj      = _proj == ProjMode.Perspective ? ProjMode.Orthographic : ProjMode.Perspective;
        _viewAngle = _proj == ProjMode.Perspective ? 45.0 * Math.PI / 180.0 : 0;
        CalcViewVolume(_gl!.Width, _gl.Height);
        var after  = ScreenToView(mouse.X, mouse.Y, _pickedPtView.Z);
        _v2wTrn   -= RotMul(after - before, _v2wRot);
    }

    // ── render ─────────────────────────────────────────────────────────

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (!_glReady)
        {
            try { _gl!.MakeCurrent(); DoGLInit(); }
            catch { return; }
        }
        try { _gl!.MakeCurrent(); Render(); _gl.SwapBuffers(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("GL Paint: " + ex); }
    }

    private void Render()
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.Viewport(0, 0, _gl!.Width, _gl.Height);
        ApplyProjection();

        // Sun direction in world space (Z-up: from above-right)
        GL.Light(LightName.Light0, LightParameter.Position,
            new[] { 0.4f, 0.3f, 1.0f, 0f });
        GL.Light(LightName.Light1, LightParameter.Position,
            new[] { -0.6f, -0.1f, 0.55f, 0f });

        DrawGrid();
        if (_mesh   != null)       DrawMesh(_mesh);
        if (_leaves.Count > 0)     DrawLeaves();
        GL.Flush();
    }

    private void DrawMesh(TreeMesh mesh)
    {
        GL.Enable(EnableCap.Lighting);
        ApplyBarkMaterial();
        foreach (var part in mesh.Parts)
        {
            var (r, g, b) = Bark[Math.Min(part.Level, Bark.Length - 1)];
            GL.Color3(r, g, b);
            var secs = part.Sections;
            for (int s = 0; s < secs.Count - 1; s++)
            {
                var lo = secs[s]; var hi = secs[s + 1];
                int nL = lo.Count, nH = hi.Count;
                if (nL == 0 || nH == 0) continue;

                if (nL == 1)
                {
                    GL.Begin(PrimitiveType.TriangleFan);
                    Emit(lo.PointAt(0));
                    for (int i = 0; i <= nH; i++) Emit(hi.PointAt(i % nH));
                    GL.End();
                }
                else if (nH == 1)
                {
                    GL.Begin(PrimitiveType.TriangleFan);
                    Emit(hi.PointAt(0));
                    for (int i = 0; i <= nL; i++) Emit(lo.PointAt(i % nL));
                    GL.End();
                }
                else
                {
                    int n = Math.Min(nL, nH);
                    GL.Begin(PrimitiveType.QuadStrip);
                    for (int i = 0; i <= n; i++) { Emit(lo.PointAt(i % nL)); Emit(hi.PointAt(i % nH)); }
                    GL.End();
                }
            }
        }
    }

    // Tree is Z-up — emit directly, no coordinate swap needed
    private static void Emit(Vertex v)
    {
        if (v.Normal is { } n) GL.Normal3(n.X, n.Y, n.Z);
        GL.Vertex3(v.Point.X, v.Point.Y, v.Point.Z);
    }

    private void DrawGrid()
    {
        GL.Disable(EnableCap.Lighting);
        GL.Color3(0.73f, 0.69f, 0.63f);   // warm gray grid on ivory
        float s = _treeH;
        GL.Begin(PrimitiveType.Lines);
        for (int i = -10; i <= 10; i++)
        {
            float t = i * s * 0.1f;
            // Grid in XY plane at Z=0 (Z-up ground)
            GL.Vertex3(-s, t, 0f); GL.Vertex3(s, t, 0f);
            GL.Vertex3(t, -s, 0f); GL.Vertex3(t, s, 0f);
        }
        GL.End();
    }

    // ── leaves ─────────────────────────────────────────────────────────

    private void DrawLeaves()
    {
        if (_leafDirty) { RebuildLeafList(); _leafDirty = false; }
        if (_leafList != 0) GL.CallList(_leafList);
    }

    private void RebuildLeafList()
    {
        if (_leafList != 0) { GL.DeleteLists(_leafList, 1); _leafList = 0; }
        if (_leaves.Count == 0) return;

        const int Segs = 10;
        var rim = new (double lx, double ly)[Segs];
        for (int i = 0; i < Segs; i++)
        {
            double a = 2 * Math.PI * i / Segs - Math.PI / 2;
            rim[i] = (_leafW * Math.Cos(a), _leafStem + _leafL * (Math.Sin(a) + 1) * 0.5);
        }
        double cx = 0, cy = _leafStem + _leafL * 0.5;

        _leafList = GL.GenLists(1);
        GL.NewList(_leafList, ListMode.Compile);
        GL.Enable(EnableCap.Lighting);
        ApplyLeafMaterial();
        GL.Begin(PrimitiveType.Triangles);
        foreach (var lq in _leaves)
        {
            var trf = lq.Trf;
            var nw  = trf.ApplyRotation(DendroKit.Core.Geom.Vector3d.ZAxis);
            float nx = (float)nw.X, ny = (float)nw.Y, nz = (float)nw.Z;

            var cp = trf.Apply(new DendroKit.Core.Geom.Vector3d(cx, cy, 0));
            var (cr, cg, cb, rr, rg, rb) = GetLeafColors(cp);
            for (int i = 0; i < Segs; i++)
            {
                var r0 = trf.Apply(new DendroKit.Core.Geom.Vector3d(rim[i].lx,          rim[i].ly,          0));
                var r1 = trf.Apply(new DendroKit.Core.Geom.Vector3d(rim[(i+1)%Segs].lx, rim[(i+1)%Segs].ly, 0));
                GL.Color3(cr, cg, cb);
                GL.Normal3(nx, ny, nz);
                GL.Vertex3(cp.X, cp.Y, cp.Z);
                GL.Color3(rr, rg, rb);
                GL.Normal3(nx, ny, nz);
                GL.Vertex3(r0.X, r0.Y, r0.Z);
                GL.Color3(rr, rg, rb);
                GL.Normal3(nx, ny, nz);
                GL.Vertex3(r1.X, r1.Y, r1.Z);
            }
        }
        GL.End();
        GL.EndList();
    }

    // ── input ──────────────────────────────────────────────────────────

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        _lastMouse = e.Location;
        _gl?.Focus();
        if (!_glReady) return;
        _gl!.MakeCurrent();
        PickDepth(e.X, e.Y);
        if (e.Button == MouseButtons.Left)  _lDown = true;
        if (e.Button == MouseButtons.Right) _rDown = true;
    }
    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)  _lDown = false;
        if (e.Button == MouseButtons.Right) _rDown = false;
    }
    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        int dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
        _lastMouse = e.Location;
        if (_lDown) { Rotate(dx, dy);                                          _gl?.Invalidate(); }
        if (_rDown) { Pan(new Point(e.X - dx, e.Y - dy), e.Location);         _gl?.Invalidate(); }
    }
    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_glReady) return;
        _gl!.MakeCurrent();
        PickDepth(e.X, e.Y);
        Zoom(e.Location, e.Delta > 0 ? 1.2f : 1f / 1.2f);
        _gl.Invalidate();
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Space || !_glReady) return;
        _gl!.MakeCurrent();
        PickDepth(_lastMouse.X, _lastMouse.Y);
        ToggleProjection(_lastMouse);
        _gl.Invalidate();
    }

    // ── helpers ────────────────────────────────────────────────────────

    private static float CalcHeight(TreeMesh? mesh)
    {
        if (mesh == null) return 5f;
        float h = 0f;
        foreach (var part in mesh.Parts)
            foreach (var sec in part.Sections)
                foreach (var v in sec.AllVertices())
                    if ((float)v.Point.Z > h) h = (float)v.Point.Z;
        return h > 0.01f ? h : 5f;
    }

    private static void ApplyBarkMaterial()
    {
        GL.Material(TriangleFace.FrontAndBack, MaterialParameter.Specular, BarkSpecular);
        GL.Material(TriangleFace.FrontAndBack, MaterialParameter.Shininess, 10f);
        GL.Material(TriangleFace.FrontAndBack, MaterialParameter.Emission, NoEmission);
    }

    private static void ApplyLeafMaterial()
    {
        GL.Material(TriangleFace.FrontAndBack, MaterialParameter.Specular, LeafSpecular);
        GL.Material(TriangleFace.FrontAndBack, MaterialParameter.Shininess, 20f);
        GL.Material(TriangleFace.FrontAndBack, MaterialParameter.Emission, NoEmission);
    }

    private (float Cr, float Cg, float Cb, float Rr, float Rg, float Rb) GetLeafColors(DendroKit.Core.Geom.Vector3d center)
    {
        float heightFactor = Math.Clamp((float)(center.Z / Math.Max(1f, _treeH)), 0f, 1f);
        float variance = LeafVariance(center);

        float centerR = Math.Clamp(0.16f + variance * 0.02f, 0f, 1f);
        float centerG = Math.Clamp(0.40f + heightFactor * 0.10f + variance * 0.05f, 0f, 1f);
        float centerB = Math.Clamp(0.11f + variance * 0.02f, 0f, 1f);

        float rimR = Math.Clamp(centerR * 0.82f, 0f, 1f);
        float rimG = Math.Clamp(centerG * 0.82f, 0f, 1f);
        float rimB = Math.Clamp(centerB * 0.82f, 0f, 1f);

        return (centerR, centerG, centerB, rimR, rimG, rimB);
    }

    private static float LeafVariance(DendroKit.Core.Geom.Vector3d p)
    {
        double n = Math.Sin(p.X * 12.9898 + p.Y * 78.233 + p.Z * 37.719);
        return (float)(0.5 + 0.5 * n);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_leafList != 0 && _glReady)
            { _gl?.MakeCurrent(); GL.DeleteLists(_leafList, 1); _leafList = 0; }
            _gl?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed record LeafQ(DendroKit.Core.Geom.Transformation Trf);

    private sealed class LeafCollector(List<LeafQ> list) : DefaultTreeTraversal
    {
        public override bool VisitLeaf(ILeaf leaf)
        { list.Add(new LeafQ(leaf.Transform)); return true; }
    }
}
