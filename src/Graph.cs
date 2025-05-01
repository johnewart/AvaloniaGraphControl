using System;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaGraphControl;

public class Graph
{
  private readonly Dictionary<string, Node> _nodes = new();
  private readonly Dictionary<object, Node?> _hierarchy = new();
  public List<Edge> Edges => _edges;

  public List<Node> Nodes => [.._nodes.Values];

  // TODO: This is probably not very efficient, but it works for now.
  public ICollection<Node> UnlinkedNodes => Nodes.FindAll(n => !Edges.Any(e => e.Tail == n || e.Head == n));

  private readonly List<Edge> _edges;
  public readonly Indexer<object, Node?> Parent;

  public Orientations Orientation { get; set; }
  public Func<object, object, int> HorizontalOrder { get; set; }
  public Func<object, object, int> VerticalOrder { get; set; }
  
  protected Graph()
  {
    _edges = new List<Edge>();
    Parent = new Indexer<object, Node?>(k => _hierarchy.GetValueOrDefault(k), (k, v) => _hierarchy[k] = v);
    Orientation = Orientations.Vertical;
    HorizontalOrder = (_, _) => 0;
    VerticalOrder = (_, _) => 0;
  }

  protected void AddNode(Node n)
  {
    if (_nodes.ContainsKey(n.Id))
    {
      return;
    }

    _nodes[n.Id] = n;
  }

  protected void AddEdge(string sourceId, string targetId, string? label = null,
    Edge.Symbol tailSymbol = Edge.Symbol.None, Edge.Symbol headSymbol = Edge.Symbol.None)
  {
    if (!_nodes.ContainsKey(sourceId))
    {
      throw new ArgumentException($"Source node with id {sourceId} does not exist.");
    }

    if (!_nodes.ContainsKey(targetId))
    {
      throw new ArgumentException($"Target node with id {targetId} does not exist.");
    }

    var sourceNode = _nodes[sourceId];
    var targetNode = _nodes[targetId];
    var edge = new Edge(sourceNode, targetNode, label ?? string.Empty, tailSymbol, headSymbol);
    _edges.Add(edge);
  }


  public enum Orientations
  {
    Vertical,
    Horizontal
  }
}
