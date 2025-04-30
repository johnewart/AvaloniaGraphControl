namespace AvaloniaGraphControl;

public class Node(string id, string label = null)
{
  public string Id { get; } = id;

  public string Label { get; } = label ?? string.Empty;
  // public Microsoft.Msagl.Drawing.Node? DNode { get; set; }

  public override string ToString() => Id;
}
