using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Microsoft.Msagl.Drawing;

namespace AvaloniaGraphControl;

public class GraphPanel : Panel
{
  public static readonly StyledProperty<Graph?> GraphProperty =
    AvaloniaProperty.Register<GraphPanel, Graph?>(nameof(Graph));

  private Microsoft.Msagl.Drawing.Graph _graphCanvas = new();
  private Dictionary<Control, Wrapper?> _controlToWrapperMapping = new();

  public Graph? Graph
  {
    get { return GetValue(GraphProperty); }
    set { SetValue(GraphProperty, value); }
  }

  public enum LayoutMethods
  {
    SugiyamaScheme,
    MDS,
    Ranking,
    IncrementalLayout
  }

  public static readonly StyledProperty<LayoutMethods> LayoutMethodProperty =
    AvaloniaProperty.Register<GraphPanel, LayoutMethods>(nameof(LayoutMethod), LayoutMethods.SugiyamaScheme);

  public LayoutMethods LayoutMethod
  {
    get { return GetValue(LayoutMethodProperty); }
    set { SetValue(LayoutMethodProperty, value); }
  }

  static GraphPanel()
  {
    GraphProperty.Changed.AddClassHandler<GraphPanel>((gp, _) =>
      gp.Build());
    LayoutMethodProperty.Changed.AddClassHandler<GraphPanel>((gp, _) =>
      gp.Build());
    AffectsMeasure<GraphPanel>(GraphProperty, LayoutMethodProperty);
  }

  public GraphPanel()
  {
    RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
    LayoutMethod = LayoutMethods.SugiyamaScheme;
  }

  private void Build()
  {
    if (Graph == null)
      return;

    _graphCanvas = new Microsoft.Msagl.Drawing.Graph
    {
      LayoutAlgorithmSettings = new Microsoft.Msagl.Layout.MDS.MdsLayoutSettings(),
      RootSubgraph = { IsVisible = false }
    };

    _controlToWrapperMapping.Clear();
    
    Children.Clear();

    var nodeWrapperMap =
      Graph.Nodes
        .Select(node => new NodeWrapper(node))
        .ToDictionary(nw => nw.WrappedObject, nw => nw);

    NodeWrapper[] parentNodes = nodeWrapperMap
      .Select(kv => Graph.Parent[kv.Key])
      .Where(pvm => pvm != null)
      .Distinct()
      .Select(pvm => nodeWrapperMap[pvm!])
      .Where(_ => true)
      .ToArray();

    var unrootedNodes =
      nodeWrapperMap
        .Select(kv => Graph.UnlinkedNodes.Contains(kv.Value.WrappedObject) ? kv.Value : null)
        .ToArray();

    var leafNodes = nodeWrapperMap.Values
      .Except(parentNodes)
      .Except(unrootedNodes)
      .ToArray();


    foreach (var nodeWrapper in parentNodes)
    {
      var sg = new Microsoft.Msagl.Drawing.Subgraph(nodeWrapper.ID);
      nodeWrapper.DNode = sg;
      var ctrl = CreateControl(nodeWrapper.WrappedObject,
        n => new TextSticker
        {
          Text = n.ToString(),
          HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
          VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        }, 1);
      _controlToWrapperMapping[ctrl] = nodeWrapper;
    }

    foreach (var nodeWrapper in parentNodes)
    {
      if (nodeWrapper is not { DNode: null })
        continue;
      
      var parent = Graph.Parent[nodeWrapper.WrappedObject];
      var pGraph = (parent == null)
        ? _graphCanvas.RootSubgraph
        : (Microsoft.Msagl.Drawing.Subgraph)nodeWrapperMap[parent].DNode;
      pGraph.AddSubgraph((Microsoft.Msagl.Drawing.Subgraph)nodeWrapper.DNode);
    }

    foreach (var nodeWrapper in unrootedNodes)
    {
      if (nodeWrapper is not { DNode: null })
        continue;
      
      var sg = new Microsoft.Msagl.Drawing.Subgraph(nodeWrapper.ID);
      nodeWrapper.DNode = sg;
      var ctrl = CreateControl(nodeWrapper.WrappedObject,
        n => new TextSticker
        {
          Text = n.ToString(),
          HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
          VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        }, 1);
      _controlToWrapperMapping[ctrl] = nodeWrapper;
    }

    foreach (var nodeWrapper in unrootedNodes)
    {
      if (nodeWrapper is not { DNode: null })
        continue;

      var subgraph = _graphCanvas.RootSubgraph;
      var dNode = _graphCanvas.AddNode(nodeWrapper.ID);
      nodeWrapper.DNode = dNode;
      subgraph.AddNode(dNode);
    }

    foreach (var edge in Graph.Edges)
    {
      var dEdge = _graphCanvas.AddEdge(nodeWrapperMap[edge.Tail].ID, nodeWrapperMap[edge.Head].ID);
      dEdge.Attr.ArrowheadAtSource = Edge.GetArrowStyle(edge.TailSymbol);
      dEdge.Attr.ArrowheadAtTarget = Edge.GetArrowStyle(edge.HeadSymbol);
      
      dEdge.Attr.AddStyle(Style.Dashed);
      dEdge.Attr.Color  = Microsoft.Msagl.Drawing.Color.Gold;
      dEdge.LabelText = "OHAI";
      dEdge.Label.FontSize = 6;
      dEdge.Label.FontColor = Microsoft.Msagl.Drawing.Color.Gold;
      dEdge.Label.IsVisible = true;
      // dEdge.Attr.LineWidth = 2;
      // dEdge.Attr.Color = Microsoft.Msagl.Drawing.Color.Gold;
      edge.DEdge = dEdge;
      
      CreateControl(edge, _ => new Connection() { Brush = Brushes.Black }, 2);
    }

    foreach (var nvm in leafNodes)
    {
      var dNode = _graphCanvas.FindNode(nvm.ID);
      nvm.DNode = dNode;
      var ctrl = CreateControl(nvm.WrappedObject, n => new TextSticker { Text = n.ToString() ?? string.Empty }, 4);
      _controlToWrapperMapping[ctrl] = nvm;
      var parent = Graph.Parent[nvm.WrappedObject];

      if (parent == null || !nodeWrapperMap.TryGetValue(parent, out var pw))
      {
        continue;
      }

      if (pw is { DNode: null })
      {
        continue;
      }

      ((Microsoft.Msagl.Drawing.Subgraph)pw.DNode).AddNode(dNode);
    }

    _graphCanvas.Attr.LayerDirection = (Graph.Orientation == Graph.Orientations.Vertical)
      ? Microsoft.Msagl.Drawing.LayerDirection.TB
      : Microsoft.Msagl.Drawing.LayerDirection.LR;
    var flowOrder = (Graph.Orientation == Graph.Orientations.Vertical) ? Graph.VerticalOrder : Graph.HorizontalOrder;
    var otherOrder = (Graph.Orientation == Graph.Orientations.Vertical) ? Graph.HorizontalOrder : Graph.VerticalOrder;

    IEnumerable<(NodeWrapper, NodeWrapper)> ComputeConstraints(Func<object, object, int> comparison) =>
      (from n1 in nodeWrapperMap
        from n2 in nodeWrapperMap
        let order = comparison(n1.Key, n2.Key)
        where order != 0
        select (order < 0) ? (n1.Value, n2.Value) : (n2.Value, n1.Value)).Distinct();

    foreach (var flowConstraint in ComputeConstraints(flowOrder))
      _graphCanvas.LayerConstraints.AddUpDownConstraint(flowConstraint.Item1.DNode, flowConstraint.Item2.DNode);
    foreach (var otherConstraint in ComputeConstraints(otherOrder))
      _graphCanvas.LayerConstraints.AddLeftRightConstraint(otherConstraint.Item1.DNode, otherConstraint.Item2.DNode);


    _graphCanvas.CreateGeometryGraph();
    _graphCanvas.GeometryGraph.RootCluster.RectangularBoundary =
      new Microsoft.Msagl.Core.Geometry.RectangularClusterBoundary();

    foreach (var evm in Graph.Edges)
    {
      if (!evm.Label.Equals(string.Empty))
      {
        var ctrl = CreateControl(evm.Label, l => new TextBlock { Text = l.ToString(), FontSize = 6 }, 3);
        _controlToWrapperMapping[ctrl] = new LabelWrapper(evm.Label, evm.DEdge.Label);
      }
    }
  }

  private Control CreateControl(object vm, Func<object, Control> getDefault, int zIndex)
  {
    var tpl = this.FindDataTemplate(vm);
    var ctrl = tpl == null ? getDefault(vm) : tpl.Build(vm);
    ctrl.DataContext = vm;
    Children.Add(ctrl);
    ctrl.ZIndex = zIndex;
    return ctrl;
  }


  protected override Size MeasureOverride(Size constraint)
  {
    if (Graph == null)
      return new Size(0, 0);
    foreach (var child in Children)
    {
      child.Measure(constraint);
      if (_controlToWrapperMapping.TryGetValue(child, out Wrapper? w))
        w.UpdateBounds(child);
    }

    try
    {
      Microsoft.Msagl.Miscellaneous.LayoutHelpers.CalculateLayout(_graphCanvas.GeometryGraph,
        _graphCanvas.LayoutAlgorithmSettings,
        null);
    }
    catch (Exception e)
    {
      Trace.TraceError("Msagl layout error {0}", e);
    }

    var graphDesiredSize = AglToAvalonia.Convert(_graphCanvas.BoundingBox.Size);
    return graphDesiredSize;
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    if (Graph == null)
      return finalSize;
    var a2a = new AglToAvalonia(_graphCanvas.BoundingBox.LeftTop);
    foreach (var child in Children)
    {
      var bbox = GetBoundingBox(child);
      if (!bbox.HasValue)
        continue;
      var childFinalSize = a2a.Convert(bbox.Value);
      child.Arrange(childFinalSize);
    }

    var graphSize = AglToAvalonia.Convert(_graphCanvas.BoundingBox.Size);
    return graphSize;
  }

  private Microsoft.Msagl.Core.Geometry.Rectangle? GetBoundingBox(Control ctrl)
  {
    try
    {
      if (_controlToWrapperMapping.TryGetValue(ctrl, out Wrapper? w))
        return w?.GetBoundingBox();

      if (ctrl is Connection { DataContext: Edge edge })
        return edge.DEdge.BoundingBox;
    }
    catch (Exception e)
    {
      Trace.TraceError("Msagl bounding box error {0}", e);
    }

    return null;
  }

  private Microsoft.Msagl.Core.Layout.LayoutAlgorithmSettings CurrentLayoutSettings =>
    LayoutMethod switch
    {
      LayoutMethods.SugiyamaScheme => new Microsoft.Msagl.Layout.Layered.SugiyamaLayoutSettings(),
      LayoutMethods.MDS => new Microsoft.Msagl.Layout.MDS.MdsLayoutSettings(),
      LayoutMethods.Ranking => new Microsoft.Msagl.Prototype.Ranking.RankingLayoutSettings(),
      LayoutMethods.IncrementalLayout => new Microsoft.Msagl.Layout.Incremental.FastIncrementalLayoutSettings(),
      _ => new Microsoft.Msagl.Layout.Incremental.FastIncrementalLayoutSettings()
    };
}


abstract class Wrapper(object wrappedObject, string id)
{
  protected object WrappedObject { get; } = wrappedObject;
  public readonly string ID = id;

  internal abstract Microsoft.Msagl.Core.Geometry.Rectangle GetBoundingBox();
  internal abstract void UpdateBounds(Control ctrl);
}

class LabelWrapper(object label, Microsoft.Msagl.Drawing.Label dLabel) : Wrapper(label, Guid.NewGuid().ToString())
{
  private readonly Microsoft.Msagl.Drawing.Label? _dLabel = dLabel;

  internal override Microsoft.Msagl.Core.Geometry.Rectangle GetBoundingBox() =>
    _dLabel?.BoundingBox ?? new Microsoft.Msagl.Core.Geometry.Rectangle(0, 0, 0, 0);

  internal override void UpdateBounds(Control ctrl)
  {
    if (_dLabel == null)
      return;
    _dLabel.Width = ctrl.DesiredSize.Width;
    _dLabel.Height = ctrl.DesiredSize.Height;
  }
}

class NodeWrapper(Node node) : Wrapper(node, node.Id)
{
  public new Node WrappedObject => (Node)base.WrappedObject;

  public Microsoft.Msagl.Drawing.Node? DNode { get; set; }

  internal override Microsoft.Msagl.Core.Geometry.Rectangle GetBoundingBox() =>
    DNode?.BoundingBox ?? new Microsoft.Msagl.Core.Geometry.Rectangle(0, 0, 0, 0);

  internal override void UpdateBounds(Control ctrl)
  {
    if (DNode?.GeometryNode == null)
      return;
    var (shape, borderRadius) =
      ctrl is TextSticker ts ? (ts.Shape, ts.BorderRadius) : (TextSticker.Shapes.Rectangle, 0);
    DNode.GeometryNode.BoundaryCurve = AglCurveFactory.Create(shape, ctrl.DesiredSize, borderRadius);
  }
}
