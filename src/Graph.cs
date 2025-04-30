
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaGraphControl
{
  public class Graph
  {
    private Dictionary<string, Node> nodes = new();
    private readonly Dictionary<object, Node?> _hierarchy = new();
    public ICollection<Edge> Edges => _edges;
    public List<Node> Nodes => new(nodes.Values);
    
    public Graph()
    {
      _edges = new List<Edge>();
      Parent = new Indexer<object, Node?>(k => _hierarchy.GetValueOrDefault(k), (k, v) => _hierarchy[k] = v);
      Orientation = Orientations.Vertical;
      HorizontalOrder = (x1, x2) => 0;
      VerticalOrder = (x1, x2) => 0;
    }

    public void AddNode(Node n)
    {
      if (nodes.ContainsKey(n.Id))
      {
        throw new ArgumentException($"Node with id {n.Id} already exists.");
      } 
      nodes[n.Id] = n;
    }

    public void AddEdge(string sourceId, string targetId)
    {
      if (!nodes.ContainsKey(sourceId))
      {
        throw new ArgumentException($"Source node with id {sourceId} does not exist.");
      }
      
      if (!nodes.ContainsKey(targetId))
      {
        throw new ArgumentException($"Target node with id {targetId} does not exist.");
      }
      var sourceNode = nodes[sourceId];
      var targetNode = nodes[targetId];
      var edge = new Edge(sourceNode, targetNode);
      _edges.Add(edge);
    }
    
    // TODO: This is probably not very efficient, but it works for now.
    public ICollection<Node> UnlinkedNodes => Nodes.FindAll(n => !Edges.Any(e => e.Tail == n || e.Head == n));
    
    private readonly ICollection<Edge> _edges;
    public readonly Indexer<object, Node?> Parent;
    public enum Orientations
    {
      Vertical,
      Horizontal
    }
    public Orientations Orientation { get; set; }
    public Func<object, object, int> HorizontalOrder { get; set; }
    public Func<object, object, int> VerticalOrder { get; set; }

  }
}
