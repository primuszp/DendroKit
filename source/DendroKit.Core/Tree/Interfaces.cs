using DendroKit.Core.Geom;

namespace DendroKit.Core.Tree;

public interface ITree
{
    long   StemCount { get; }
    long   LeafCount { get; }
    double Height    { get; }
    double Width     { get; }
    string Species   { get; }
    double Scale     { get; }
    string LeafShape { get; }
    double LeafWidth { get; }
    double LeafLength{ get; }
    double LeafStemLength { get; }
    void   TraverseTree(ITreeTraversal traversal);
}

public interface IStem
{
    int           Level       { get; }
    double        Length      { get; }
    double        BaseRadius  { get; }
    double        PeakRadius  { get; }
    Transformation Transform  { get; }
    bool          IsClone     { get; }
    int           CloneSectionOffset { get; }
    string        TreePosition { get; }
    IEnumerable<IStemSection> Sections();
    bool          TraverseTree(ITreeTraversal traversal);
}

public interface IStemSection
{
    int            Index          { get; }
    double         Length         { get; }
    Transformation Transform      { get; }
    double         LowerRadius    { get; }
    double         UpperRadius    { get; }
    Vector3d       LowerPosition  { get; }
    Vector3d       UpperPosition  { get; }
    bool           IsLastStemSegment { get; }
    Vector3d[]     GetSectionPoints();
}

public interface ILeaf
{
    Transformation Transform { get; }
    bool TraverseTree(ITreeTraversal traversal);
}

public interface ITreeTraversal
{
    bool EnterTree(ITree tree);
    bool LeaveTree(ITree tree);
    bool EnterStem(IStem stem);
    bool LeaveStem(IStem stem);
    bool VisitLeaf(ILeaf leaf);
}

public abstract class DefaultTreeTraversal : ITreeTraversal
{
    public virtual bool EnterTree(ITree tree)  => true;
    public virtual bool LeaveTree(ITree tree)  => true;
    public virtual bool EnterStem(IStem stem)  => true;
    public virtual bool LeaveStem(IStem stem)  => true;
    public virtual bool VisitLeaf(ILeaf leaf)  => true;
}

public class StemCounter : DefaultTreeTraversal
{
    public long Count { get; private set; }
    public override bool EnterStem(IStem stem) { Count++; return true; }
}

public class LeafCounter : DefaultTreeTraversal
{
    public long Count { get; private set; }
    public override bool VisitLeaf(ILeaf leaf) { Count++; return true; }
}